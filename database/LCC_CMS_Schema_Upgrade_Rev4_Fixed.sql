/* =========================================================================
   LCC-CMS — Schema upgrade Rev 3 → Rev 4 (FIXED)
   File: database/LCC_CMS_Schema_Upgrade_Rev4_Fixed.sql
   Date: 2026-09-08
   Prerequisite: LCCCMSDB full backup (2026-09-07 or later).

   Why this file exists
     LCC_CMS_Schema_Upgrade_Rev4.sql compiled CREATE INDEX on
     dbo.admission_documents / dbo.learning_materials and UPDATE
     dbo.submissions.original_file_name in the SAME batch as the
     CREATE TABLE / ALTER TABLE that introduce those names.

     SQL Server compiles the whole batch before any statement runs.
     IF OBJECT_ID / COL_LENGTH do not skip compilation. The batch
     failed with 208 / 207, XACT_ABORT rolled back, and nothing
     was applied.

   Fix
     Keep one transaction. Run CREATE INDEX and the submissions
     backfill via sp_executesql so they compile AFTER the DDL
     has executed. Verification uses catalog views only (no
     SELECT FROM new tables).

   Safety
     Idempotent (IF OBJECT_ID / COL_LENGTH / sys.indexes)
     SET XACT_ABORT ON; BEGIN TRANSACTION; COMMIT
     No DROP / DELETE of business rows
   ========================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.admissions', N'U') IS NULL
    THROW 50001, N'Rev4 abort: dbo.admissions is missing. Restore Rev 3 schema first.', 1;
IF OBJECT_ID(N'dbo.course_allocations', N'U') IS NULL
    THROW 50002, N'Rev4 abort: dbo.course_allocations is missing. Restore Rev 3 schema first.', 1;
IF OBJECT_ID(N'dbo.staff', N'U') IS NULL
    THROW 50003, N'Rev4 abort: dbo.staff is missing. Restore Rev 3 schema first.', 1;
IF OBJECT_ID(N'dbo.assignments', N'U') IS NULL
    THROW 50004, N'Rev4 abort: dbo.assignments is missing. Restore Rev 3 schema first.', 1;
IF OBJECT_ID(N'dbo.documents', N'U') IS NULL
    THROW 50005, N'Rev4 abort: dbo.documents is missing. Restore Rev 3 schema first.', 1;
IF OBJECT_ID(N'dbo.submissions', N'U') IS NULL
    THROW 50006, N'Rev4 abort: dbo.submissions is missing. Restore Rev 3 schema first.', 1;

BEGIN TRANSACTION;

    DECLARE @cnt_admissions     INT = (SELECT COUNT(*) FROM dbo.admissions);
    DECLARE @cnt_allocations    INT = (SELECT COUNT(*) FROM dbo.course_allocations);
    DECLARE @cnt_staff          INT = (SELECT COUNT(*) FROM dbo.staff);
    DECLARE @cnt_assignments    INT = (SELECT COUNT(*) FROM dbo.assignments);
    DECLARE @cnt_documents      INT = (SELECT COUNT(*) FROM dbo.documents);
    DECLARE @cnt_submissions    INT = (SELECT COUNT(*) FROM dbo.submissions);
    DECLARE @sql NVARCHAR(MAX);

    /* ---------------------------------------------------------------------
       1. admission_documents
       Entity: AdmissionDocument  Controller: AdmissionsController
       --------------------------------------------------------------------- */
    IF OBJECT_ID(N'dbo.admission_documents', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.admission_documents (
            admission_document_id INT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_admission_documents PRIMARY KEY,
            admission_id          INT             NOT NULL,
            document_type         NVARCHAR(100)   NOT NULL,
            storage_key           NVARCHAR(500)   NOT NULL,
            original_file_name    NVARCHAR(255)   NOT NULL,
            content_type          NVARCHAR(255)   NULL,
            file_size             BIGINT          NOT NULL,
            uploaded_at           DATETIME2       NOT NULL
                CONSTRAINT DF_admission_documents_uploaded_at DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK__admission_documents__admission
                FOREIGN KEY (admission_id)
                REFERENCES dbo.admissions(admission_id)
                ON DELETE CASCADE
        );
        PRINT N'Created dbo.admission_documents.';
    END
    ELSE
        PRINT N'Skipped dbo.admission_documents (already exists).';

    /* Dynamic SQL: CREATE INDEX is compiled at EXEC time, after CREATE TABLE. */
    IF OBJECT_ID(N'dbo.admission_documents', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_admission_documents_admission'
              AND object_id = OBJECT_ID(N'dbo.admission_documents')
       )
    BEGIN
        SET @sql = N'CREATE INDEX IX_admission_documents_admission
            ON dbo.admission_documents(admission_id);';
        EXEC sys.sp_executesql @sql;
    END

    /* ---------------------------------------------------------------------
       2. learning_materials
       Entity: LearningMaterial  Controller: LearningController
       --------------------------------------------------------------------- */
    IF OBJECT_ID(N'dbo.learning_materials', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.learning_materials (
            learning_material_id  INT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_learning_materials PRIMARY KEY,
            allocation_id         INT             NOT NULL,
            title                 NVARCHAR(150)   NOT NULL,
            storage_key           NVARCHAR(500)   NOT NULL,
            original_file_name    NVARCHAR(255)   NOT NULL,
            content_type          NVARCHAR(255)   NULL,
            file_size             BIGINT          NOT NULL,
            uploaded_at           DATETIME2       NOT NULL
                CONSTRAINT DF_learning_materials_uploaded_at DEFAULT SYSUTCDATETIME(),
            uploaded_by_staff_id  INT             NULL,
            CONSTRAINT FK__learning_materials__allocation
                FOREIGN KEY (allocation_id)
                REFERENCES dbo.course_allocations(allocation_id),
            CONSTRAINT FK__learning_materials__uploaded_by
                FOREIGN KEY (uploaded_by_staff_id)
                REFERENCES dbo.staff(staff_id)
                ON DELETE SET NULL
        );
        PRINT N'Created dbo.learning_materials.';
    END
    ELSE
        PRINT N'Skipped dbo.learning_materials (already exists).';

    IF OBJECT_ID(N'dbo.learning_materials', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_learning_materials_allocation'
              AND object_id = OBJECT_ID(N'dbo.learning_materials')
       )
    BEGIN
        SET @sql = N'CREATE INDEX IX_learning_materials_allocation
            ON dbo.learning_materials(allocation_id);';
        EXEC sys.sp_executesql @sql;
    END

    IF OBJECT_ID(N'dbo.learning_materials', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_learning_materials_uploaded_by'
              AND object_id = OBJECT_ID(N'dbo.learning_materials')
       )
    BEGIN
        SET @sql = N'CREATE INDEX IX_learning_materials_uploaded_by
            ON dbo.learning_materials(uploaded_by_staff_id);';
        EXEC sys.sp_executesql @sql;
    END

    /* ---------------------------------------------------------------------
       3–6. New columns (ALTER TABLE ADD does not bind the new name later
            in this batch, so static DDL is safe.)
       --------------------------------------------------------------------- */
    IF COL_LENGTH(N'dbo.assignments', N'allow_late_submissions') IS NULL
    BEGIN
        ALTER TABLE dbo.assignments ADD allow_late_submissions BIT NOT NULL
            CONSTRAINT DF_assignments_allow_late_submissions DEFAULT (0);
        PRINT N'Added dbo.assignments.allow_late_submissions.';
    END
    ELSE
        PRINT N'Skipped assignments.allow_late_submissions (already exists).';

    IF COL_LENGTH(N'dbo.documents', N'content_type') IS NULL
    BEGIN
        ALTER TABLE dbo.documents ADD content_type NVARCHAR(255) NULL;
        PRINT N'Added dbo.documents.content_type.';
    END
    ELSE
        PRINT N'Skipped documents.content_type (already exists).';

    IF COL_LENGTH(N'dbo.submissions', N'original_file_name') IS NULL
    BEGIN
        ALTER TABLE dbo.submissions ADD original_file_name NVARCHAR(255) NOT NULL
            CONSTRAINT DF_submissions_original_file_name DEFAULT (N'');
        PRINT N'Added dbo.submissions.original_file_name.';
    END
    ELSE
        PRINT N'Skipped submissions.original_file_name (already exists).';

    IF COL_LENGTH(N'dbo.submissions', N'content_type') IS NULL
    BEGIN
        ALTER TABLE dbo.submissions ADD content_type NVARCHAR(255) NULL;
        PRINT N'Added dbo.submissions.content_type.';
    END
    ELSE
        PRINT N'Skipped submissions.content_type (already exists).';

    /* Dynamic SQL: column original_file_name is bound at EXEC time. */
    IF COL_LENGTH(N'dbo.submissions', N'original_file_name') IS NOT NULL
    BEGIN
        SET @sql = N'
            UPDATE dbo.submissions
            SET original_file_name =
                RIGHT(file_url, CHARINDEX(N''/'', REVERSE(file_url) + N''/'') - 1)
            WHERE original_file_name = N''''
              AND file_url IS NOT NULL
              AND LEN(file_url) > 0;';
        EXEC sys.sp_executesql @sql;
    END

    IF (SELECT COUNT(*) FROM dbo.admissions) <> @cnt_admissions
        THROW 50011, N'Rev4 abort: admissions row count changed.', 1;
    IF (SELECT COUNT(*) FROM dbo.course_allocations) <> @cnt_allocations
        THROW 50012, N'Rev4 abort: course_allocations row count changed.', 1;
    IF (SELECT COUNT(*) FROM dbo.staff) <> @cnt_staff
        THROW 50013, N'Rev4 abort: staff row count changed.', 1;
    IF (SELECT COUNT(*) FROM dbo.assignments) <> @cnt_assignments
        THROW 50014, N'Rev4 abort: assignments row count changed.', 1;
    IF (SELECT COUNT(*) FROM dbo.documents) <> @cnt_documents
        THROW 50015, N'Rev4 abort: documents row count changed.', 1;
    IF (SELECT COUNT(*) FROM dbo.submissions) <> @cnt_submissions
        THROW 50016, N'Rev4 abort: submissions row count changed.', 1;

COMMIT TRANSACTION;
PRINT N'Rev 4 schema upgrade committed.';

GO

/* =========================================================================
   Post-upgrade verification (catalog views only — no SELECT FROM new tables)
   After a successful COMMIT, exists_flag must be 1 for every row.
   ========================================================================= */

SELECT
    t.object_name,
    CASE WHEN OBJECT_ID(t.object_id_name, N'U') IS NOT NULL THEN 1 ELSE 0 END AS exists_flag,
    (
        SELECT SUM(p.rows)
        FROM sys.partitions AS p
        WHERE p.object_id = OBJECT_ID(t.object_id_name)
          AND p.index_id IN (0, 1)
    ) AS row_count
FROM (VALUES
    (N'admission_documents', N'dbo.admission_documents'),
    (N'learning_materials', N'dbo.learning_materials'),
    (N'admissions (parent)', N'dbo.admissions'),
    (N'course_allocations (parent)', N'dbo.course_allocations'),
    (N'staff (parent)', N'dbo.staff'),
    (N'assignments (preserved)', N'dbo.assignments'),
    (N'documents (preserved)', N'dbo.documents'),
    (N'submissions (preserved)', N'dbo.submissions')
) AS t(object_name, object_id_name);

SELECT
    c.column_ref,
    CASE WHEN COL_LENGTH(c.table_name, c.column_name) IS NOT NULL THEN 1 ELSE 0 END AS exists_flag
FROM (VALUES
    (N'assignments.allow_late_submissions', N'dbo.assignments', N'allow_late_submissions'),
    (N'documents.content_type', N'dbo.documents', N'content_type'),
    (N'submissions.original_file_name', N'dbo.submissions', N'original_file_name'),
    (N'submissions.content_type', N'dbo.submissions', N'content_type')
) AS c(column_ref, table_name, column_name);

SELECT i.name AS index_name, OBJECT_NAME(i.object_id) AS table_name
FROM sys.indexes AS i
WHERE i.name IN (
    N'IX_admission_documents_admission',
    N'IX_learning_materials_allocation',
    N'IX_learning_materials_uploaded_by'
)
ORDER BY table_name, index_name;

SELECT fk.name AS foreign_key_name, OBJECT_NAME(fk.parent_object_id) AS table_name
FROM sys.foreign_keys AS fk
WHERE fk.name IN (
    N'FK__admission_documents__admission',
    N'FK__learning_materials__allocation',
    N'FK__learning_materials__uploaded_by'
)
ORDER BY table_name, foreign_key_name;

GO

/* =========================================================================
   Deployment
   1. Confirm LCCCMSDB backup is restorable (prior run applied nothing).
   2. Idle the API. USE LCCCMSDB (or sqlcmd -d LCCCMSDB).
   3. Run this entire file (not the original Rev4 script).
   4. Expect PRINT: Rev 4 schema upgrade committed.
   5. Verification: exists_flag = 1 for both new tables and four columns.
      New table row_count = 0 on first apply.
   6. Re-run is a no-op. Do not run the original Rev4 file.

   Rollback: restore the backup. This file has no DROP/DELETE of Rev 4 objects.
   ========================================================================= */
