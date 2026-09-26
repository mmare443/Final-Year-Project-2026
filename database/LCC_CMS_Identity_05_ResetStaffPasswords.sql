/* List staff sign-in identities, then set every staff password to LccCms!2026.
   Hash is ASP.NET Identity V3 for that password.
   must_change_password = 1, so the first sign-in must choose a new password.
   Does not print password hashes. Does not change student passwords. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @PasswordHash nvarchar(500) =
    N'AQAAAAIAAYagAAAAEFOjrzhqUCXTfUaEoRRth3WZA6m+cYidyw4wosGEelIO1WIoTYlUHzf6kiHFulxl0Q==';

SELECT
    u.user_id AS ID,
    s.full_name AS Name,
    u.role AS Role,
    s.staff_number AS StaffId,
    u.email AS Email,
    u.status AS Status,
    u.must_change_password AS MustChangePassword,
    CASE WHEN u.password_hash IS NULL OR LTRIM(RTRIM(u.password_hash)) = N'' THEN 0 ELSE 1 END AS HasPassword
FROM dbo.users AS u
INNER JOIN dbo.staff AS s ON s.staff_id = u.user_id
ORDER BY s.full_name, u.user_id;

UPDATE u
SET
    u.password_hash = @PasswordHash,
    u.must_change_password = 1
FROM dbo.users AS u
INNER JOIN dbo.staff AS s ON s.staff_id = u.user_id;

SELECT
    u.user_id AS ID,
    s.full_name AS Name,
    s.staff_number AS StaffId,
    u.email AS Email,
    u.status AS Status,
    u.must_change_password AS MustChangePassword,
    CASE WHEN u.password_hash = @PasswordHash THEN 1 ELSE 0 END AS ResetToDefault
FROM dbo.users AS u
INNER JOIN dbo.staff AS s ON s.staff_id = u.user_id
ORDER BY s.full_name, u.user_id;
