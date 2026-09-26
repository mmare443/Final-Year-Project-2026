/* Restores emails, password hashes, and must_change_password saved by script 02.
   Deletes office users that script 02 created. Does nothing if the backup table is absent. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.identity_rollback_20260924', N'U') IS NULL
BEGIN
    RAISERROR(N'No identity_rollback_20260924 table. Nothing to roll back.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

DELETE u
FROM dbo.users AS u
INNER JOIN dbo.identity_rollback_20260924 AS b ON b.user_id = u.user_id
WHERE b.created_by_script = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.students AS s WHERE s.student_id = u.user_id)
  AND NOT EXISTS (SELECT 1 FROM dbo.staff AS s WHERE s.staff_id = u.user_id);

UPDATE u
SET
    u.email = b.email,
    u.password_hash = b.password_hash,
    u.must_change_password = b.must_change_password
FROM dbo.users AS u
INNER JOIN dbo.identity_rollback_20260924 AS b ON b.user_id = u.user_id
WHERE b.created_by_script = 0;

COMMIT TRANSACTION;

DROP TABLE dbo.identity_rollback_20260924;

IF OBJECT_ID(N'dbo.fn_lcc_letters', N'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_lcc_letters;
