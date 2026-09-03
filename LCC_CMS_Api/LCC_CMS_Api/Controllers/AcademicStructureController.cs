using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M3 — Academic Structure (Module Specification).
///
/// Faculties, departments, programmes, courses, academic years, semesters,
/// and course allocations are persisted through EF Core.
///
/// [Authorize(Policy = "RegistrarAdminOnly")] goes back on every
/// write endpoint below once AuthEnabled=true — per the spec, only
/// Admin/Registrar may create or modify academic structure.
///
/// NOTE: per spec, these entities only support Create/Read/Update — no
/// Delete endpoints are provided here, matching the Module Specification's
/// "Associated Database Entities" list exactly.
/// </summary>
[ApiController]
[Route("api/academic-structure")]
public class AcademicStructureController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;

    public AcademicStructureController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // --- Faculties ---
    [HttpGet("faculties")]
    public async Task<IActionResult> GetFaculties()
    {
        var faculties = await _dbContext.Faculties
            .AsNoTracking()
            .Select(f => new { f.FacultyId, f.FacultyName })
            .ToListAsync();

        return Ok(faculties);
    }

    [HttpPost("faculties")]
    public async Task<IActionResult> CreateFaculty([FromBody] Faculty request)
    {
        var f = new Faculty { FacultyName = request.FacultyName };
        _dbContext.Faculties.Add(f);
        await _dbContext.SaveChangesAsync();
        return Ok(f);
    }

    [HttpPut("faculties/{id}")]
    public async Task<IActionResult> UpdateFaculty(int id, [FromBody] Faculty request)
    {
        var f = await _dbContext.Faculties.FindAsync(id);
        if (f is null) return NotFound();
        f.FacultyName = request.FacultyName;
        await _dbContext.SaveChangesAsync();
        return Ok(f);
    }

    // --- Departments ---
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Select(d => new { d.DepartmentId, d.DepartmentName, d.FacultyId })
            .ToListAsync();

        return Ok(departments);
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] Department request)
    {
        if (!await _dbContext.Faculties.AnyAsync(f => f.FacultyId == request.FacultyId))
        {
            return BadRequest("Faculty not found.");
        }

        var d = new Department
        {
            DepartmentName = request.DepartmentName,
            FacultyId = request.FacultyId,
        };
        _dbContext.Departments.Add(d);
        await _dbContext.SaveChangesAsync();
        return Ok(d);
    }

    [HttpPut("departments/{id}")]
    public async Task<IActionResult> UpdateDepartment(int id, [FromBody] Department request)
    {
        var d = await _dbContext.Departments.FindAsync(id);
        if (d is null) return NotFound();

        if (!await _dbContext.Faculties.AnyAsync(f => f.FacultyId == request.FacultyId))
        {
            return BadRequest("Faculty not found.");
        }

        d.DepartmentName = request.DepartmentName;
        d.FacultyId = request.FacultyId;
        await _dbContext.SaveChangesAsync();
        return Ok(d);
    }

    // --- Programmes ---
    [HttpGet("programmes")]
    public async Task<IActionResult> GetProgrammes()
    {
        var programmes = await _dbContext.Programmes
            .AsNoTracking()
            .Select(p => new { p.ProgrammeId, p.ProgrammeName, p.DepartmentId, p.DurationYears })
            .ToListAsync();

        return Ok(programmes);
    }

    [HttpPost("programmes")]
    public async Task<IActionResult> CreateProgramme([FromBody] Programme request)
    {
        if (!await _dbContext.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId))
        {
            return BadRequest("Department not found.");
        }

        var p = new Programme
        {
            ProgrammeName = request.ProgrammeName,
            DepartmentId = request.DepartmentId,
            DurationYears = request.DurationYears,
        };
        _dbContext.Programmes.Add(p);
        await _dbContext.SaveChangesAsync();
        return Ok(p);
    }

    [HttpPut("programmes/{id}")]
    public async Task<IActionResult> UpdateProgramme(int id, [FromBody] Programme request)
    {
        var p = await _dbContext.Programmes.FindAsync(id);
        if (p is null) return NotFound();

        if (!await _dbContext.Departments.AnyAsync(d => d.DepartmentId == request.DepartmentId))
        {
            return BadRequest("Department not found.");
        }

        p.ProgrammeName = request.ProgrammeName;
        p.DepartmentId = request.DepartmentId;
        p.DurationYears = request.DurationYears;
        await _dbContext.SaveChangesAsync();
        return Ok(p);
    }

    // --- Courses ---
    [HttpGet("courses")]
    public async Task<IActionResult> GetCourses()
    {
        var courses = await _dbContext.Courses
            .AsNoTracking()
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.ProgrammeId,
                c.CreditValue,
                c.YearLevel,
                c.SemesterNo,
                c.IsCore,
                c.PrerequisiteCourseId,
            })
            .ToListAsync();

        return Ok(courses);
    }

    [HttpPost("courses")]
    public async Task<IActionResult> CreateCourse([FromBody] Course request)
    {
        if (!await _dbContext.Programmes.AnyAsync(p => p.ProgrammeId == request.ProgrammeId))
        {
            return BadRequest("Programme not found.");
        }

        if (request.PrerequisiteCourseId is not null &&
            !await _dbContext.Courses.AnyAsync(c => c.CourseId == request.PrerequisiteCourseId))
        {
            return BadRequest("Prerequisite course not found.");
        }

        var c = new Course
        {
            CourseCode = request.CourseCode,
            CourseName = request.CourseName,
            ProgrammeId = request.ProgrammeId,
            CreditValue = request.CreditValue,
            YearLevel = request.YearLevel,
            SemesterNo = request.SemesterNo,
            IsCore = request.IsCore,
            PrerequisiteCourseId = request.PrerequisiteCourseId,
        };
        _dbContext.Courses.Add(c);
        await _dbContext.SaveChangesAsync();
        return Ok(c);
    }

    [HttpPut("courses/{id}")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] Course request)
    {
        var c = await _dbContext.Courses.FindAsync(id);
        if (c is null) return NotFound();

        if (!await _dbContext.Programmes.AnyAsync(p => p.ProgrammeId == request.ProgrammeId))
        {
            return BadRequest("Programme not found.");
        }

        if (request.PrerequisiteCourseId is not null &&
            !await _dbContext.Courses.AnyAsync(x => x.CourseId == request.PrerequisiteCourseId))
        {
            return BadRequest("Prerequisite course not found.");
        }

        c.CourseCode = request.CourseCode;
        c.CourseName = request.CourseName;
        c.ProgrammeId = request.ProgrammeId;
        c.CreditValue = request.CreditValue;
        c.YearLevel = request.YearLevel;
        c.SemesterNo = request.SemesterNo;
        c.IsCore = request.IsCore;
        c.PrerequisiteCourseId = request.PrerequisiteCourseId;
        await _dbContext.SaveChangesAsync();
        return Ok(c);
    }

    // --- Academic Years ---
    [HttpGet("academic-years")]
    public async Task<IActionResult> GetAcademicYears()
    {
        var years = await _dbContext.AcademicYears
            .AsNoTracking()
            .Select(y => new { y.AcademicYearId, y.YearName, y.StartDate, y.EndDate })
            .ToListAsync();

        return Ok(years);
    }

    [HttpPost("academic-years")]
    public async Task<IActionResult> CreateAcademicYear([FromBody] AcademicYear request)
    {
        var y = new AcademicYear
        {
            YearName = request.YearName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
        };
        _dbContext.AcademicYears.Add(y);
        await _dbContext.SaveChangesAsync();
        return Ok(y);
    }

    // --- Semesters ---
    [HttpGet("semesters")]
    public async Task<IActionResult> GetSemesters()
    {
        var semesters = await _dbContext.Semesters
            .AsNoTracking()
            .Select(s => new
            {
                s.SemesterId,
                s.AcademicYearId,
                s.SemesterName,
                s.SemesterNo,
                s.StartDate,
                s.EndDate,
                s.IsActive,
            })
            .ToListAsync();

        return Ok(semesters);
    }

    [HttpPost("semesters")]
    public async Task<IActionResult> CreateSemester([FromBody] Semester request)
    {
        if (!await _dbContext.AcademicYears.AnyAsync(y => y.AcademicYearId == request.AcademicYearId))
        {
            return BadRequest("Academic year not found.");
        }

        var s = new Semester
        {
            AcademicYearId = request.AcademicYearId,
            SemesterName = string.IsNullOrWhiteSpace(request.SemesterName)
                ? $"Semester {request.SemesterNo}"
                : request.SemesterName,
            SemesterNo = request.SemesterNo,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false, // never active on creation — set via the dedicated activate endpoint
        };
        _dbContext.Semesters.Add(s);
        await _dbContext.SaveChangesAsync();
        return Ok(s);
    }

    // Business rule: only one semester may be active at any time.
    [HttpPut("semesters/{id}/activate")]
    public async Task<IActionResult> ActivateSemester(int id)
    {
        var target = await _dbContext.Semesters.FindAsync(id);
        if (target is null) return NotFound();

        var currentlyActive = await _dbContext.Semesters.Where(s => s.IsActive).ToListAsync();
        foreach (var s in currentlyActive)
        {
            s.IsActive = false;
        }
        await _dbContext.SaveChangesAsync();

        target.IsActive = true;
        await _dbContext.SaveChangesAsync();
        return Ok(target);
    }

    // --- Course Allocations ---
    [HttpGet("course-allocations")]
    public async Task<IActionResult> GetCourseAllocations()
    {
        var allocations = await _dbContext.CourseAllocations
            .AsNoTracking()
            .Select(a => new { a.AllocationId, a.CourseId, a.SemesterId, a.StaffId })
            .ToListAsync();

        return Ok(allocations);
    }

    [HttpPost("course-allocations")]
    public async Task<IActionResult> CreateCourseAllocation([FromBody] CourseAllocation request)
    {
        if (!await _dbContext.Courses.AnyAsync(c => c.CourseId == request.CourseId))
        {
            return BadRequest("Course not found.");
        }

        if (!await _dbContext.Semesters.AnyAsync(s => s.SemesterId == request.SemesterId))
        {
            return BadRequest("Semester not found.");
        }

        if (!await _dbContext.Staff.AnyAsync(s => s.StaffId == request.StaffId))
        {
            return BadRequest("Staff not found.");
        }

        var a = new CourseAllocation
        {
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            StaffId = request.StaffId,
        };
        _dbContext.CourseAllocations.Add(a);
        await _dbContext.SaveChangesAsync();
        return Ok(a);
    }
}
