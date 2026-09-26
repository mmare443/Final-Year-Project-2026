/* Identity audit. Read only. Run this on LCCCMSDB before any update.
   Students sign in with student_number. Staff and office users sign in with email.
   There is no separate ForcePasswordChange column. must_change_password is that flag. */
SET NOCOUNT ON;

SELECT
    u.user_id AS ID,
    COALESCE(st.full_name, sf.full_name, N'(no profile)') AS Name,
    u.role AS Role,
    CASE
        WHEN st.student_id IS NOT NULL THEN st.student_number
        ELSE u.email
    END AS Username,
    u.email AS Email,
    CASE WHEN u.status = N'Active' THEN 1 ELSE 0 END AS Active,
    u.status AS Status,
    CASE
        WHEN st.student_id IS NOT NULL THEN N'Student'
        WHEN u.email IN (
            N'registration@lccbportal.org',
            N'admissions@lccbportal.org',
            N'principal@lccbportal.org',
            N'finance@lccbportal.org',
            N'noreply@lccbportal.org') THEN N'Office'
        WHEN sf.staff_id IS NOT NULL THEN N'Staff'
        ELSE N'User'
    END AS AccountKind,
    st.student_number AS StudentId
FROM dbo.users AS u
LEFT JOIN dbo.students AS st ON st.student_id = u.user_id
LEFT JOIN dbo.staff AS sf ON sf.staff_id = u.user_id
ORDER BY AccountKind, Name, u.user_id;

SELECT
    SUM(CASE WHEN st.student_id IS NOT NULL THEN 1 ELSE 0 END) AS TotalStudents,
    SUM(CASE WHEN sf.staff_id IS NOT NULL
              AND u.email NOT IN (
                  N'registration@lccbportal.org',
                  N'admissions@lccbportal.org',
                  N'principal@lccbportal.org',
                  N'finance@lccbportal.org',
                  N'noreply@lccbportal.org')
             THEN 1 ELSE 0 END) AS TotalStaff,
    SUM(CASE WHEN u.email IN (
            N'registration@lccbportal.org',
            N'admissions@lccbportal.org',
            N'principal@lccbportal.org',
            N'finance@lccbportal.org',
            N'noreply@lccbportal.org') THEN 1 ELSE 0 END) AS TotalOfficeAccounts,
    SUM(CASE WHEN u.email IS NULL OR LTRIM(RTRIM(u.email)) = N'' THEN 1 ELSE 0 END) AS AccountsMissingEmail,
    SUM(CASE WHEN u.status = N'Active' THEN 1 ELSE 0 END) AS ActiveUsers
FROM dbo.users AS u
LEFT JOIN dbo.students AS st ON st.student_id = u.user_id
LEFT JOIN dbo.staff AS sf ON sf.staff_id = u.user_id;
