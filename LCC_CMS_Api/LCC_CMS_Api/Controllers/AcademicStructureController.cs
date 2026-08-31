using Microsoft.AspNetCore.Mvc;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M3 — Academic Structure (Module Specification).
///
/// SKELETON: in-memory placeholder data, same pattern as every other
/// controller in this project. Actors: Admin/Registrar only — this whole
/// controller is the foundation the rest of the system (M4 onward) reads
/// from, so its lists are exposed as internal static so sibling
/// controllers (RegistrationsController) can read them directly. This is
/// a deliberate in-memory-stage simplification; once EF Core/a real
/// database exists, that becomes a normal foreign-key relationship
/// instead of a cross-class static reference.
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
    internal static readonly List<Faculty> _faculties = new()
    {
        new Faculty { Id = 1, Name = "Faculty of Ministry" },
        new Faculty { Id = 2, Name = "Faculty of Agriculture" },
        new Faculty { Id = 3, Name = "Faculty of Business Administration" },
    };

    internal static readonly List<Department> _departments = new()
    {
        new Department { Id = 1, Name = "Applied Ministry", FacultyId = 1 },
        new Department { Id = 2, Name = "Tropical Agriculture", FacultyId = 2 },
        new Department { Id = 3, Name = "Business Administration and Management", FacultyId = 3 },
    };

    // Matches Section 1 of the real LCCB Application Form (used in M1's
    // Apply.jsx too) — this becomes the authoritative source going
    // forward; M1's own copy stays as-is per its own scope, not swapped
    // over automatically here.
    internal static readonly List<ProgrammeRecord> _programmes = new()
    {
        new ProgrammeRecord { Id = 1, Name = "Diploma in Applied Ministry", DepartmentId = 1, DurationYears = 3 },
        new ProgrammeRecord { Id = 2, Name = "Diploma in Tropical Agriculture", DepartmentId = 2, DurationYears = 3 },
        new ProgrammeRecord { Id = 3, Name = "Diploma in Business Administration and Management", DepartmentId = 3, DurationYears = 3 },
        new ProgrammeRecord { Id = 4, Name = "Certificate in Applied Ministry", DepartmentId = 1, DurationYears = 2 },
        new ProgrammeRecord { Id = 5, Name = "Certificate in Tropical Agriculture", DepartmentId = 2, DurationYears = 2 },
        new ProgrammeRecord { Id = 6, Name = "Certificate in Business Administration and Management", DepartmentId = 3, DurationYears = 2 },
    };

    internal static readonly List<CourseRecord> _courses = new()
    {
        new CourseRecord { Id = 1, Code = "BAM101", Name = "Introduction to Business", ProgrammeId = 3, CreditValue = 10, YearLevel = 1, SemesterNumber = 1, PrerequisiteCourseId = null },
        new CourseRecord { Id = 2, Code = "BAM102", Name = "Principles of Accounting", ProgrammeId = 3, CreditValue = 10, YearLevel = 1, SemesterNumber = 1, PrerequisiteCourseId = null },
        new CourseRecord { Id = 3, Code = "BAM201", Name = "Financial Management", ProgrammeId = 3, CreditValue = 10, YearLevel = 2, SemesterNumber = 1, PrerequisiteCourseId = 2 },
    };

    internal static readonly List<AcademicYearRecord> _academicYears = new()
    {
        new AcademicYearRecord { Id = 1, Name = "2026" },
    };

    internal static readonly List<SemesterRecord> _semesters = new()
    {
        new SemesterRecord { Id = 1, AcademicYearId = 1, SemesterNumber = 1, StartDate = "2026-02-01", EndDate = "2026-06-30", IsActive = true },
        new SemesterRecord { Id = 2, AcademicYearId = 1, SemesterNumber = 2, StartDate = "2026-07-15", EndDate = "2026-11-30", IsActive = false },
    };

    internal static readonly List<CourseAllocationRecord> _courseAllocations = new()
    {
        new CourseAllocationRecord { Id = 1, CourseId = 1, SemesterId = 1, LecturerName = "Mr. J. Kaupa" },
        new CourseAllocationRecord { Id = 2, CourseId = 2, SemesterId = 1, LecturerName = "Mrs. A. Temu" },
    };

    private static int _nextId = 100;

    // --- Faculties ---
    [HttpGet("faculties")]
    public ActionResult<IEnumerable<Faculty>> GetFaculties() => Ok(_faculties);

    [HttpPost("faculties")]
    public ActionResult<Faculty> CreateFaculty([FromBody] Faculty request)
    {
        var f = new Faculty { Id = _nextId++, Name = request.Name };
        _faculties.Add(f);
        return Ok(f);
    }

    [HttpPut("faculties/{id}")]
    public ActionResult<Faculty> UpdateFaculty(int id, [FromBody] Faculty request)
    {
        var f = _faculties.FirstOrDefault(x => x.Id == id);
        if (f is null) return NotFound();
        f.Name = request.Name;
        return Ok(f);
    }

    // --- Departments ---
    [HttpGet("departments")]
    public ActionResult<IEnumerable<Department>> GetDepartments() => Ok(_departments);

    [HttpPost("departments")]
    public ActionResult<Department> CreateDepartment([FromBody] Department request)
    {
        var d = new Department { Id = _nextId++, Name = request.Name, FacultyId = request.FacultyId };
        _departments.Add(d);
        return Ok(d);
    }

    [HttpPut("departments/{id}")]
    public ActionResult<Department> UpdateDepartment(int id, [FromBody] Department request)
    {
        var d = _departments.FirstOrDefault(x => x.Id == id);
        if (d is null) return NotFound();
        d.Name = request.Name;
        d.FacultyId = request.FacultyId;
        return Ok(d);
    }

    // --- Programmes ---
    [HttpGet("programmes")]
    public ActionResult<IEnumerable<ProgrammeRecord>> GetProgrammes() => Ok(_programmes);

    [HttpPost("programmes")]
    public ActionResult<ProgrammeRecord> CreateProgramme([FromBody] ProgrammeRecord request)
    {
        var p = new ProgrammeRecord { Id = _nextId++, Name = request.Name, DepartmentId = request.DepartmentId, DurationYears = request.DurationYears };
        _programmes.Add(p);
        return Ok(p);
    }

    [HttpPut("programmes/{id}")]
    public ActionResult<ProgrammeRecord> UpdateProgramme(int id, [FromBody] ProgrammeRecord request)
    {
        var p = _programmes.FirstOrDefault(x => x.Id == id);
        if (p is null) return NotFound();
        p.Name = request.Name;
        p.DepartmentId = request.DepartmentId;
        p.DurationYears = request.DurationYears;
        return Ok(p);
    }

    // --- Courses ---
    [HttpGet("courses")]
    public ActionResult<IEnumerable<CourseRecord>> GetCourses() => Ok(_courses);

    [HttpPost("courses")]
    public ActionResult<CourseRecord> CreateCourse([FromBody] CourseRecord request)
    {
        var c = new CourseRecord
        {
            Id = _nextId++,
            Code = request.Code,
            Name = request.Name,
            ProgrammeId = request.ProgrammeId,
            CreditValue = request.CreditValue,
            YearLevel = request.YearLevel,
            SemesterNumber = request.SemesterNumber,
            PrerequisiteCourseId = request.PrerequisiteCourseId,
        };
        _courses.Add(c);
        return Ok(c);
    }

    [HttpPut("courses/{id}")]
    public ActionResult<CourseRecord> UpdateCourse(int id, [FromBody] CourseRecord request)
    {
        var c = _courses.FirstOrDefault(x => x.Id == id);
        if (c is null) return NotFound();
        c.Code = request.Code;
        c.Name = request.Name;
        c.ProgrammeId = request.ProgrammeId;
        c.CreditValue = request.CreditValue;
        c.YearLevel = request.YearLevel;
        c.SemesterNumber = request.SemesterNumber;
        c.PrerequisiteCourseId = request.PrerequisiteCourseId;
        return Ok(c);
    }

    // --- Academic Years ---
    [HttpGet("academic-years")]
    public ActionResult<IEnumerable<AcademicYearRecord>> GetAcademicYears() => Ok(_academicYears);

    [HttpPost("academic-years")]
    public ActionResult<AcademicYearRecord> CreateAcademicYear([FromBody] AcademicYearRecord request)
    {
        var y = new AcademicYearRecord { Id = _nextId++, Name = request.Name };
        _academicYears.Add(y);
        return Ok(y);
    }

    // --- Semesters ---
    [HttpGet("semesters")]
    public ActionResult<IEnumerable<SemesterRecord>> GetSemesters() => Ok(_semesters);

    [HttpPost("semesters")]
    public ActionResult<SemesterRecord> CreateSemester([FromBody] SemesterRecord request)
    {
        var s = new SemesterRecord
        {
            Id = _nextId++,
            AcademicYearId = request.AcademicYearId,
            SemesterNumber = request.SemesterNumber,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = false, // never active on creation — set via the dedicated activate endpoint
        };
        _semesters.Add(s);
        return Ok(s);
    }

    // Business rule: only one semester may be active at any time.
    [HttpPut("semesters/{id}/activate")]
    public ActionResult<SemesterRecord> ActivateSemester(int id)
    {
        var target = _semesters.FirstOrDefault(x => x.Id == id);
        if (target is null) return NotFound();

        foreach (var s in _semesters) s.IsActive = false;
        target.IsActive = true;
        return Ok(target);
    }

    // --- Course Allocations ---
    [HttpGet("course-allocations")]
    public ActionResult<IEnumerable<CourseAllocationRecord>> GetCourseAllocations() => Ok(_courseAllocations);

    [HttpPost("course-allocations")]
    public ActionResult<CourseAllocationRecord> CreateCourseAllocation([FromBody] CourseAllocationRecord request)
    {
        var a = new CourseAllocationRecord
        {
            Id = _nextId++,
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            LecturerName = request.LecturerName,
        };
        _courseAllocations.Add(a);
        return Ok(a);
    }
}

public class Faculty
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int FacultyId { get; set; }
}

public class ProgrammeRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int DepartmentId { get; set; }
    public int DurationYears { get; set; }
}

public class CourseRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int ProgrammeId { get; set; }
    public int CreditValue { get; set; }
    public int YearLevel { get; set; }
    public int SemesterNumber { get; set; }
    public int? PrerequisiteCourseId { get; set; }
}

public class AcademicYearRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class SemesterRecord
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public int SemesterNumber { get; set; }
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public bool IsActive { get; set; }
}

public class CourseAllocationRecord
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int SemesterId { get; set; }
    public string LecturerName { get; set; } = "";
}
