using LCC_CMS_Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Services;

/// <summary>
/// Derives completed course attempts and GPA/CGPA from published grades.
/// A completed attempt is an Approved registration whose allocation has
/// assessments totalling 100% weight and a published letter for every
/// assessment. Course letter comes from the weighted percentage, then
/// grade_scale supplies the point value (A=4 … F=0).
/// </summary>
public class CourseResultService
{
    private const decimal BandA = 80m;
    private const decimal BandB = 70m;
    private const decimal BandC = 60m;
    private const decimal BandD = 50m;

    private readonly LccCmsDbContext _dbContext;

    public CourseResultService(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GpaRecord?> GetGpaAsync(string studentNumber, int? semesterId)
    {
        var student = await FindStudentAsync(studentNumber);
        if (student is null) return null;

        var filterSemesterId = semesterId;
        if (filterSemesterId is null)
        {
            filterSemesterId = await _dbContext.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => (int?)s.SemesterId)
                .FirstOrDefaultAsync();
        }

        var attempts = await LoadCompletedAttemptsAsync(student.StudentId);
        if (filterSemesterId is not null)
        {
            attempts = attempts.Where(a => a.SemesterId == filterSemesterId).ToList();
        }

        return ToGpaRecord(student.StudentNumber, attempts, filterSemesterId);
    }

    public async Task<CgpaRecord?> GetCgpaAsync(string studentNumber)
    {
        var student = await FindStudentAsync(studentNumber);
        if (student is null) return null;

        var attempts = await LoadCompletedAttemptsAsync(student.StudentId);
        var gpa = ToGpaRecord(student.StudentNumber, attempts, semesterId: null);
        return new CgpaRecord
        {
            StudentId = gpa.StudentId,
            Cgpa = gpa.Gpa,
            CompletedCourseCount = gpa.CompletedCourseCount,
            TotalCredits = gpa.TotalCredits,
        };
    }

    public async Task<TranscriptRecord?> GetTranscriptAsync(string studentNumber)
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .Include(s => s.Programme)
            .Include(s => s.Admission)
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
        if (student is null) return null;

        var attempts = await LoadCompletedAttemptsAsync(student.StudentId);

        int? activeSemesterId = await _dbContext.Semesters
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => (int?)s.SemesterId)
            .FirstOrDefaultAsync();

        var semesterAttempts = activeSemesterId is null
            ? attempts
            : attempts.Where(a => a.SemesterId == activeSemesterId).ToList();

        var gpa = ToGpaRecord(student.StudentNumber, semesterAttempts, activeSemesterId);
        var cgpa = ToGpaRecord(student.StudentNumber, attempts, semesterId: null);

        return new TranscriptRecord
        {
            StudentNumber = student.StudentNumber,
            StudentName = student.Admission?.ApplicantName ?? student.StudentNumber,
            Programme = student.Programme.ProgrammeName,
            Gpa = gpa.Gpa,
            Cgpa = cgpa.Gpa,
            CompletedCourses = attempts
                .OrderBy(a => a.CourseCode)
                .ThenBy(a => a.SemesterId)
                .Select(a => new TranscriptCourseRecord
                {
                    CourseCode = a.CourseCode,
                    CourseName = a.CourseName,
                    CourseLetter = a.Letter,
                    Credits = a.CreditValue,
                })
                .ToList(),
        };
    }

    private async Task<Student?> FindStudentAsync(string studentNumber)
    {
        return await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
    }

    private async Task<List<CompletedAttempt>> LoadCompletedAttemptsAsync(int studentId)
    {
        var scale = await _dbContext.GradeScales
            .AsNoTracking()
            .ToDictionaryAsync(s => s.GradeLetter, s => s.GradeValue, StringComparer.OrdinalIgnoreCase);

        var registrations = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Status == "Approved")
            .Include(r => r.Allocation)
                .ThenInclude(a => a.Course)
            .Include(r => r.Allocation)
                .ThenInclude(a => a.Assessments)
            .ToListAsync();

        var assessmentIds = registrations
            .SelectMany(r => r.Allocation.Assessments.Select(a => a.AssessmentId))
            .Distinct()
            .ToList();

        var grades = await _dbContext.Grades
            .AsNoTracking()
            .Where(g => g.StudentId == studentId && assessmentIds.Contains(g.AssessmentId))
            .ToListAsync();
        var gradesByAssessment = grades.ToDictionary(g => g.AssessmentId);

        var completed = new List<CompletedAttempt>();
        foreach (var registration in registrations)
        {
            var assessments = registration.Allocation.Assessments.ToList();
            if (assessments.Count == 0) continue;

            var weightSum = assessments.Sum(a => a.WeightPercent);
            if (weightSum != 100m) continue;

            decimal weightedPercent = 0m;
            var complete = true;
            foreach (var assessment in assessments)
            {
                if (!gradesByAssessment.TryGetValue(assessment.AssessmentId, out var grade)
                    || !grade.Published
                    || string.IsNullOrWhiteSpace(grade.GradeLetter))
                {
                    complete = false;
                    break;
                }

                var max = assessment.MaxMarks;
                var componentPercent = max <= 0 ? 0m : 100m * grade.MarksObtained / max;
                weightedPercent += componentPercent * (assessment.WeightPercent / 100m);
            }

            if (!complete) continue;

            var letter = LetterFromPercent(weightedPercent);
            if (!scale.TryGetValue(letter, out var gradeValue)) continue;

            var course = registration.Allocation.Course;
            completed.Add(new CompletedAttempt
            {
                SemesterId = registration.Allocation.SemesterId,
                CourseId = course.CourseId,
                CourseCode = course.CourseCode,
                CourseName = course.CourseName,
                Letter = letter,
                CreditValue = course.CreditValue,
                GradeValue = gradeValue,
            });
        }

        return completed;
    }

    private static GpaRecord ToGpaRecord(string studentNumber, List<CompletedAttempt> attempts, int? semesterId)
    {
        var credits = attempts.Sum(a => a.CreditValue);
        decimal? gpa = null;
        if (credits > 0)
        {
            var points = attempts.Sum(a => a.GradeValue * a.CreditValue);
            gpa = decimal.Round(points / credits, 2, MidpointRounding.AwayFromZero);
        }

        return new GpaRecord
        {
            StudentId = studentNumber,
            Gpa = gpa,
            SemesterId = semesterId,
            CompletedCourseCount = attempts.Count,
            TotalCredits = credits,
        };
    }

    /// <summary>
    /// Completed, published course attempts for registration rules (FR-4.2 / FR-4.3).
    /// Passing letters are A–D; F is completed but not a pass.
    /// </summary>
    public async Task<List<CompletedCourseAttempt>> GetCompletedCoursesAsync(int studentId)
    {
        var attempts = await LoadCompletedAttemptsAsync(studentId);
        return attempts.Select(a => new CompletedCourseAttempt
        {
            CourseId = a.CourseId,
            CourseCode = a.CourseCode,
            CourseName = a.CourseName,
            Letter = a.Letter,
        }).ToList();
    }

    public static bool IsPassingLetter(string? letter)
    {
        return letter is "A" or "B" or "C" or "D";
    }

    internal static string LetterFromPercent(decimal percent)
    {
        if (percent >= BandA) return "A";
        if (percent >= BandB) return "B";
        if (percent >= BandC) return "C";
        if (percent >= BandD) return "D";
        return "F";
    }

    private sealed class CompletedAttempt
    {
        public int SemesterId { get; set; }
        public int CourseId { get; set; }
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Letter { get; set; } = "";
        public decimal CreditValue { get; set; }
        public decimal GradeValue { get; set; }
    }
}

public class GpaRecord
{
    public string StudentId { get; set; } = "";
    public decimal? Gpa { get; set; }
    public int? SemesterId { get; set; }
    public int CompletedCourseCount { get; set; }
    public decimal TotalCredits { get; set; }
}

public class CgpaRecord
{
    public string StudentId { get; set; } = "";
    public decimal? Cgpa { get; set; }
    public int CompletedCourseCount { get; set; }
    public decimal TotalCredits { get; set; }
}

public class TranscriptRecord
{
    public string StudentNumber { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string Programme { get; set; } = "";
    public decimal? Gpa { get; set; }
    public decimal? Cgpa { get; set; }
    public List<TranscriptCourseRecord> CompletedCourses { get; set; } = new();
}

public class TranscriptCourseRecord
{
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string CourseLetter { get; set; } = "";
    public decimal Credits { get; set; }
}

public class CompletedCourseAttempt
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string Letter { get; set; } = "";
}
