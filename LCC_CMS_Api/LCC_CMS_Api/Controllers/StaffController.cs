using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M9 Phase 1 — Faculty &amp; Staff Management. Creates a <c>users</c> row
/// and matching <c>staff</c> subtype (staff_id = user_id). Faculty is
/// projected from the assigned department. Phase 3 workload reads
/// existing <c>course_allocations</c> (no new tables).
/// </summary>
[ApiController]
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private static readonly HashSet<string> StaffSqlRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        RoleNames.Lecturer,
        RoleNames.HoD,
        RoleNames.RegistrarAdminSql,
        RoleNames.ManagementPrincipalSql,
    };

    private readonly LccCmsDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        LccCmsDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtSettings,
        ICurrentUser currentUser,
        ILogger<StaffController> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("workload")]
    public async Task<ActionResult<IEnumerable<StaffWorkloadRecord>>> GetWorkloads(
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeWorkloadAsync(cancellationToken);
        if (access.Error is not null) return access.Error;

        var resolvedSemester = await ResolveWorkloadSemesterIdAsync(semesterId, cancellationToken);
        if (resolvedSemester.Error is not null) return resolvedSemester.Error;

        var staff = await WorkloadStaffQuery(access.HoDDepartmentId)
            .ToListAsync(cancellationToken);
        await HydrateStaffUsersAsync(staff, cancellationToken);

        return Ok(staff.Select(s => ToWorkload(s, resolvedSemester.SemesterId)));
    }

    [HttpGet("{id:int}/workload")]
    public async Task<ActionResult<StaffWorkloadRecord>> GetWorkloadById(
        int id,
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeWorkloadAsync(cancellationToken);
        if (access.Error is not null) return access.Error;

        var resolvedSemester = await ResolveWorkloadSemesterIdAsync(semesterId, cancellationToken);
        if (resolvedSemester.Error is not null) return resolvedSemester.Error;

        var staff = await WorkloadStaffQuery(access.HoDDepartmentId)
            .FirstOrDefaultAsync(s => s.StaffId == id, cancellationToken);
        if (staff is null)
        {
            var exists = await _dbContext.Staff.AsNoTracking()
                .AnyAsync(s => s.StaffId == id, cancellationToken);
            return exists ? StatusCode(StatusCodes.Status403Forbidden) : NotFound();
        }

        await HydrateStaffUsersAsync(new[] { staff }, cancellationToken);
        return Ok(ToWorkload(staff, resolvedSemester.SemesterId));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffRecord>>> GetAll(CancellationToken cancellationToken)
    {
        var staff = await StaffGraph()
            .AsNoTracking()
            .OrderBy(s => s.StaffId)
            .ToListAsync(cancellationToken);
        await HydrateStaffUsersAsync(staff, cancellationToken);

        return Ok(staff
            .OrderBy(s => s.StaffNavigation.Role)
            .ThenBy(s => s.StaffNavigation.Email)
            .Select(ToRecord));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffRecord>> GetById(int id, CancellationToken cancellationToken)
    {
        var staff = await StaffGraph()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StaffId == id, cancellationToken);
        if (staff is null) return NotFound();
        await HydrateStaffUsersAsync(new[] { staff }, cancellationToken);
        return Ok(ToRecord(staff));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<StaffRecord>> UpdateMe(
        [FromBody] StaffSelfUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.StaffId is null)
        {
            return Unauthorized();
        }

        var fullName = request.FullName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("Full name is required.");
        }

        if (fullName.Length > 150)
        {
            return BadRequest("Full name must be 150 characters or fewer.");
        }

        var details = string.IsNullOrWhiteSpace(request.EmploymentDetails)
            ? null
            : request.EmploymentDetails.Trim();
        if (details is { Length: > 500 })
        {
            return BadRequest("Contact details must be 500 characters or fewer.");
        }

        var staff = await StaffGraph()
            .FirstOrDefaultAsync(s => s.StaffId == _currentUser.StaffId.Value, cancellationToken);
        if (staff is null) return NotFound();

        staff.FullName = fullName;
        staff.EmploymentDetails = details;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await HydrateStaffUsersAsync(new[] { staff }, cancellationToken);
        return Ok(ToRecord(staff));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPost]
    public async Task<ActionResult<StaffRecord>> Create(
        [FromBody] StaffCreateRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(
            request.FullName, request.Email, request.Role, request.JobTitle, request.EmploymentDetails);
        if (error is not null) return error;

        var email = request.Email.Trim();
        var sqlRole = RoleNames.ToSqlRole(request.Role.Trim());
        if (!StaffSqlRoles.Contains(sqlRole))
        {
            return BadRequest(
                "Role must be Lecturer, HoD, Registrar/Admin (or RegistrarAdmin), or Management/Principal (or ManagementPrincipal).");
        }

        if (!await _dbContext.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken))
        {
            return BadRequest("Department not found.");
        }

        if (await _dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Conflict("A user with this email already exists.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Email = email,
            EntraId = Guid.NewGuid().ToString("D"),
            Role = sqlRole,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            MustChangePassword = true,
        };

        if (!string.IsNullOrWhiteSpace(_jwtSettings.LabPassword))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, _jwtSettings.LabPassword);
        }

        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WriteFailed(ex, "create user");
        }

        var staff = new Staff
        {
            StaffId = user.UserId,
            StaffNumber = await NextStaffNumberAsync(cancellationToken),
            FullName = request.FullName.Trim(),
            DepartmentId = request.DepartmentId,
            JobTitle = request.JobTitle.Trim(),
            EmploymentDetails = string.IsNullOrWhiteSpace(request.EmploymentDetails)
                ? null
                : request.EmploymentDetails.Trim(),
            StaffNavigation = user,
        };
        _dbContext.Staff.Add(staff);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return WriteFailed(ex, "create staff");
        }

        var created = await StaffGraph()
            .AsNoTracking()
            .FirstAsync(s => s.StaffId == staff.StaffId, cancellationToken);
        await HydrateStaffUsersAsync(new[] { created }, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.StaffId }, ToRecord(created));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<StaffRecord>> Update(
        int id,
        [FromBody] StaffUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateWrite(
            request.FullName, request.Email, request.Role, request.JobTitle, request.EmploymentDetails);
        if (error is not null) return error;

        var email = request.Email.Trim();
        var sqlRole = RoleNames.ToSqlRole(request.Role.Trim());
        if (!StaffSqlRoles.Contains(sqlRole))
        {
            return BadRequest(
                "Role must be Lecturer, HoD, Registrar/Admin (or RegistrarAdmin), or Management/Principal (or ManagementPrincipal).");
        }

        var staff = await _dbContext.Staff
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.StaffId == id, cancellationToken);
        if (staff is null) return NotFound();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == id, cancellationToken);
        if (user is null) return NotFound();
        staff.StaffNavigation = user;

        if (!await _dbContext.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId, cancellationToken))
        {
            return BadRequest("Department not found.");
        }

        if (await _dbContext.Users.AnyAsync(
                u => u.Email == email && u.UserId != id, cancellationToken))
        {
            return Conflict("A user with this email already exists.");
        }

        staff.StaffNavigation.Email = email;
        staff.StaffNavigation.Role = sqlRole;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (!status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Status must be Active or Inactive.");
            }

            staff.StaffNavigation.Status = status.Equals("Active", StringComparison.OrdinalIgnoreCase)
                ? "Active"
                : "Inactive";
        }

        staff.FullName = request.FullName.Trim();
        staff.DepartmentId = request.DepartmentId;
        staff.JobTitle = request.JobTitle.Trim();
        staff.EmploymentDetails = string.IsNullOrWhiteSpace(request.EmploymentDetails)
            ? null
            : request.EmploymentDetails.Trim();

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return WriteFailed(ex, "update staff");
        }

        var updated = await StaffGraph()
            .AsNoTracking()
            .FirstAsync(s => s.StaffId == id, cancellationToken);
        await HydrateStaffUsersAsync(new[] { updated }, cancellationToken);
        return Ok(ToRecord(updated));
    }

    private IQueryable<Staff> StaffGraph()
    {
        return _dbContext.Staff
            .Include(s => s.Department)
                .ThenInclude(d => d.Faculty);
    }

    private IQueryable<Staff> WorkloadStaffQuery(int? hodDepartmentId)
    {
        var query = _dbContext.Staff
            .AsNoTracking()
            .Include(s => s.Department)
            .Include(s => s.CourseAllocations)
                .ThenInclude(a => a.Course)
            .AsQueryable();

        if (hodDepartmentId is int departmentId)
        {
            query = query.Where(s => s.DepartmentId == departmentId);
        }

        return query.OrderBy(s => s.StaffId);
    }

    private async Task HydrateStaffUsersAsync(
        IReadOnlyCollection<Staff> staff,
        CancellationToken cancellationToken)
    {
        if (staff.Count == 0) return;

        var ids = staff.Select(s => s.StaffId).Distinct().ToList();
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Email, u.Role, u.Status, u.EntraId })
            .ToListAsync(cancellationToken);
        var byId = users.ToDictionary(u => u.UserId);

        foreach (var row in staff)
        {
            if (!byId.TryGetValue(row.StaffId, out var user))
            {
                row.StaffNavigation = new User
                {
                    UserId = row.StaffId,
                    Email = "",
                    Role = "",
                    Status = "Inactive",
                    EntraId = "",
                };
                continue;
            }
            row.StaffNavigation = new User
            {
                UserId = user.UserId,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                EntraId = user.EntraId,
            };
        }
    }

    private async Task<(int? HoDDepartmentId, ActionResult? Error)> AuthorizeWorkloadAsync(
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return (null, Unauthorized());
        }

        var role = RoleNames.ToPolicyRole(_currentUser.Role);
        if (role.Equals(RoleNames.RegistrarAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        if (role.Equals(RoleNames.HoD, StringComparison.OrdinalIgnoreCase))
        {
            if (_currentUser.StaffId is not int staffId)
            {
                return (null, StatusCode(StatusCodes.Status403Forbidden));
            }

            var departmentId = await _dbContext.Staff
                .AsNoTracking()
                .Where(s => s.StaffId == staffId)
                .Select(s => (int?)s.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);
            if (departmentId is null)
            {
                return (null, StatusCode(StatusCodes.Status403Forbidden));
            }

            return (departmentId, null);
        }

        return (null, StatusCode(StatusCodes.Status403Forbidden));
    }

    private async Task<(int SemesterId, ActionResult? Error)> ResolveWorkloadSemesterIdAsync(
        int? semesterId,
        CancellationToken cancellationToken)
    {
        if (semesterId is int requested)
        {
            var exists = await _dbContext.Semesters
                .AsNoTracking()
                .AnyAsync(s => s.SemesterId == requested, cancellationToken);
            if (!exists) return (0, BadRequest("Semester not found."));
            return (requested, null);
        }

        var activeId = await _dbContext.Semesters
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => (int?)s.SemesterId)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeId is null)
        {
            return (0, BadRequest("semesterId is required when no semester is active."));
        }

        return (activeId.Value, null);
    }

    private static StaffWorkloadRecord ToWorkload(Staff staff, int semesterId)
    {
        var allocations = staff.CourseAllocations
            .Where(a => a.SemesterId == semesterId)
            .OrderBy(a => a.Course.CourseCode)
            .ToList();

        return new StaffWorkloadRecord
        {
            StaffId = staff.StaffId,
            StaffNumber = staff.StaffNumber,
            FullName = staff.FullName,
            Email = staff.StaffNavigation.Email,
            Role = RoleNames.ToPolicyRole(staff.StaffNavigation.Role),
            JobTitle = staff.JobTitle,
            Department = staff.Department.DepartmentName,
            DepartmentId = staff.DepartmentId,
            SemesterId = semesterId,
            CourseCount = allocations.Count,
            AllocatedCourses = allocations.Select(a => new AllocatedCourseRecord
            {
                AllocationId = a.AllocationId,
                CourseId = a.CourseId,
                CourseCode = a.Course.CourseCode,
                CourseName = a.Course.CourseName,
            }).ToList(),
        };
    }

    private async Task<string> NextStaffNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"STF-{year}-";
        var existing = await _dbContext.Staff
            .AsNoTracking()
            .Where(s => s.StaffNumber.StartsWith(prefix))
            .Select(s => s.StaffNumber)
            .ToListAsync(cancellationToken);

        var maxSeq = 0;
        foreach (var number in existing)
        {
            var suffix = number.Length > prefix.Length ? number[prefix.Length..] : "";
            if (int.TryParse(suffix, out var seq) && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        if (maxSeq >= 999)
        {
            throw new InvalidOperationException($"Staff ID sequence for {year} is exhausted.");
        }

        return $"{prefix}{(maxSeq + 1).ToString("D3")}";
    }

    private ActionResult? ValidateWrite(
        string? fullName, string? email, string? role, string? jobTitle, string? employmentDetails)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return BadRequest("Full name is required.");
        if (fullName.Trim().Length > 150) return BadRequest("Full name must be 150 characters or fewer.");
        if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");
        if (email.Trim().Length > 255) return BadRequest("Email must be 255 characters or fewer.");
        if (string.IsNullOrWhiteSpace(role)) return BadRequest("Role is required.");
        if (string.IsNullOrWhiteSpace(jobTitle)) return BadRequest("Job title is required.");
        if (jobTitle.Trim().Length > 100) return BadRequest("Job title must be 100 characters or fewer.");
        if (employmentDetails is { Length: > 500 })
        {
            return BadRequest("Additional staff information must be 500 characters or fewer.");
        }

        return null;
    }

    private ObjectResult WriteFailed(Exception ex, string operation)
    {
        var sql = FindSqlException(ex);
        var detail = sql?.Message ?? ex.GetBaseException().Message;
        _logger.LogError(ex, "Staff {Operation} failed: {Detail}", operation, detail);
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            $"Staff {operation} failed: {detail}");
    }

    private static SqlException? FindSqlException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql) return sql;
        }

        return null;
    }

    private static StaffRecord ToRecord(Staff staff)
    {
        var user = staff.StaffNavigation;
        var department = staff.Department;
        var faculty = department.Faculty;
        return new StaffRecord
        {
            StaffId = staff.StaffId,
            StaffNumber = staff.StaffNumber,
            FullName = staff.FullName,
            UserId = user.UserId,
            Email = user.Email,
            Role = RoleNames.ToPolicyRole(user.Role),
            RoleSql = user.Role,
            Status = user.Status,
            JobTitle = staff.JobTitle,
            EmploymentDetails = staff.EmploymentDetails,
            DepartmentId = staff.DepartmentId,
            DepartmentName = department.DepartmentName,
            FacultyId = department.FacultyId,
            FacultyName = faculty.FacultyName,
        };
    }
}

public class StaffRecord
{
    public int StaffId { get; set; }
    public string StaffNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string RoleSql { get; set; } = "";
    public string Status { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string? EmploymentDetails { get; set; }
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "";
    public int FacultyId { get; set; }
    public string FacultyName { get; set; } = "";
}

public class StaffCreateRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public int DepartmentId { get; set; }
    public string JobTitle { get; set; } = "";
    public string? EmploymentDetails { get; set; }
}

public class StaffUpdateRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? Status { get; set; }
    public int DepartmentId { get; set; }
    public string JobTitle { get; set; } = "";
    public string? EmploymentDetails { get; set; }
}

public class StaffSelfUpdateRequest
{
    public string FullName { get; set; } = "";
    public string? EmploymentDetails { get; set; }
}

public class StaffWorkloadRecord
{
    public int StaffId { get; set; }
    public string StaffNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string Department { get; set; } = "";
    public int DepartmentId { get; set; }
    public int SemesterId { get; set; }
    public int CourseCount { get; set; }
    public IReadOnlyList<AllocatedCourseRecord> AllocatedCourses { get; set; } =
        Array.Empty<AllocatedCourseRecord>();
}

public class AllocatedCourseRecord
{
    public int AllocationId { get; set; }
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
}
