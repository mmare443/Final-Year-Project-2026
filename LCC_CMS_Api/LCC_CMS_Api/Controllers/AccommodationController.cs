using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M10 Phase 2 — allocate and vacate rooms. Occupancy is the count of
/// Active <c>accommodation_records</c> for a room (same rule as Phase 1).
/// Welfare cases are Phase 3.
/// </summary>
[ApiController]
[Route("api/accommodation")]
public class AccommodationController : ControllerBase
{
    public const string ActiveStatus = "Active";
    public const string VacatedStatus = "Vacated";

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AccommodationController(LccCmsDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccommodationRecordDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var records = await RecordQuery()
            .OrderByDescending(r => r.DateAllocated)
            .ThenByDescending(r => r.AccommodationId)
            .ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<IEnumerable<AccommodationRecordDto>>> GetByStudent(
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Students.AnyAsync(s => s.StudentId == studentId, cancellationToken))
        {
            return NotFound();
        }

        var records = await RecordQuery()
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.DateAllocated)
            .ThenByDescending(r => r.AccommodationId)
            .ToListAsync(cancellationToken);
        return Ok(records);
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPost("allocate")]
    public async Task<ActionResult<AccommodationRecordDto>> Allocate(
        [FromBody] AccommodationAllocateRequest request,
        CancellationToken cancellationToken)
    {
        var staff = await RequireStaffAsync(cancellationToken);
        if (staff.Error is not null) return staff.Error;

        if (request.StudentId <= 0) return BadRequest("Student is required.");
        if (request.RoomId <= 0) return BadRequest("Room is required.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var studentExists = await _dbContext.Students
            .AnyAsync(s => s.StudentId == request.StudentId, cancellationToken);
        if (!studentExists)
        {
            return BadRequest("Student not found.");
        }

        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.RoomId == request.RoomId, cancellationToken);
        if (room is null)
        {
            return BadRequest("Room not found.");
        }

        var hasActive = await _dbContext.AccommodationRecords
            .AnyAsync(
                a => a.StudentId == request.StudentId && a.Status == ActiveStatus,
                cancellationToken);
        if (hasActive)
        {
            return Conflict("This student already has an active room allocation.");
        }

        var occupants = await _dbContext.AccommodationRecords
            .CountAsync(
                a => a.RoomId == request.RoomId && a.Status == ActiveStatus,
                cancellationToken);
        if (occupants >= room.Capacity)
        {
            return Conflict("This room has no available spaces.");
        }

        var allocatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = new AccommodationRecord
        {
            StudentId = request.StudentId,
            RoomId = request.RoomId,
            Status = ActiveStatus,
            AllocatedBy = staff.StaffId,
            DateAllocated = allocatedOn,
            DateVacated = null,
        };
        _dbContext.AccommodationRecords.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateActive(ex))
        {
            return Conflict("This student already has an active room allocation.");
        }

        var created = await RecordQuery()
            .FirstAsync(r => r.AccommodationId == record.AccommodationId, cancellationToken);
        return Created($"/api/accommodation/{record.AccommodationId}", created);
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("{id:int}/vacate")]
    public async Task<ActionResult<AccommodationRecordDto>> Vacate(
        int id,
        CancellationToken cancellationToken)
    {
        var staff = await RequireStaffAsync(cancellationToken);
        if (staff.Error is not null) return staff.Error;

        var record = await _dbContext.AccommodationRecords
            .FirstOrDefaultAsync(a => a.AccommodationId == id, cancellationToken);
        if (record is null) return NotFound();

        if (!string.Equals(record.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("This allocation is already vacated.");
        }

        record.Status = VacatedStatus;
        record.DateVacated = DateOnly.FromDateTime(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await RecordQuery()
            .FirstAsync(r => r.AccommodationId == id, cancellationToken);
        return Ok(updated);
    }

    private IQueryable<AccommodationRecordDto> RecordQuery()
    {
        return _dbContext.AccommodationRecords
            .AsNoTracking()
            .Select(a => new AccommodationRecordDto
            {
                AccommodationId = a.AccommodationId,
                StudentId = a.StudentId,
                StudentNumber = a.Student.StudentNumber,
                StudentEmail = a.Student.StudentNavigation.Email,
                RoomId = a.RoomId,
                RoomNumber = a.Room.RoomNumber,
                HostelId = a.Room.HostelId,
                HostelName = a.Room.Hostel.HostelName,
                Status = a.Status,
                AllocatedBy = a.AllocatedBy,
                AllocatedByEmail = a.AllocatedByNavigation.StaffNavigation.Email,
                DateAllocated = a.DateAllocated,
                DateVacated = a.DateVacated,
            });
    }

    private async Task<(int StaffId, ActionResult? Error)> RequireStaffAsync(
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return (0, Unauthorized());
        }

        if (_currentUser.StaffId is not int staffId)
        {
            return (0, StatusCode(StatusCodes.Status403Forbidden));
        }

        return (staffId, null);
    }

    private static bool IsDuplicateActive(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql
            && sql.Number is 2601 or 2627;
    }
}

public class AccommodationAllocateRequest
{
    public int StudentId { get; set; }
    public int RoomId { get; set; }
}

public class AccommodationRecordDto
{
    public int AccommodationId { get; set; }
    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string StudentEmail { get; set; } = "";
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = "";
    public int HostelId { get; set; }
    public string HostelName { get; set; } = "";
    public string Status { get; set; } = "";
    public int AllocatedBy { get; set; }
    public string AllocatedByEmail { get; set; } = "";
    public DateOnly DateAllocated { get; set; }
    public DateOnly? DateVacated { get; set; }
}
