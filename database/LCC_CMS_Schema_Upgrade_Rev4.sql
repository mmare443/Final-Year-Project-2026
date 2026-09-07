/* =========================================================================
   LCC-CMS — Schema upgrade Rev 3 → Rev 4 (T-SQL / SQL Server)
   Date: 2026-09-07
   Prerequisite: LCCCMSDB full backup completed 2026-09-07.

   Purpose
     Align an existing database built from database/LCC_CMS_Schema.sql (Rev 3)
     with the current EF model in LccCmsDbContext. The project does not use
     EF migrations. This script is the supported upgrade path.

   Scope (schema reconciliation audit)
     New tables:  admission_documents, learning_materials
     New columns: assignments.allow_late_submissions
                  documents.content_type
                  submissions.original_file_name
                  submissions.content_type

   Safety
     - Idempotent: IF OBJECT_ID / COL_LENGTH / sys.indexes
     - Transactional: SET XACT_ABORT ON; BEGIN TRANSACTION; COMMIT
     - Does not DROP objects, does not delete rows
     - Does not modify application code

   Target
     Database: LCCCMSDB  (select the database in SSMS or sqlcmd -d LCCCMSDB)
   ========================================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* -------------------------------------------------------------------------
   Pre-flight: Rev 3 parents must exist (otherwise FKs cannot be created).
   ------------------------------------------------------------------------- */
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

    /* Baseline row counts (must be unchanged after COMMIT). */
    DECLARE @cnt_admissions     INT = (SELECT COUNT(*) FROM dbo.admissions);
    DECLARE @cnt_allocations    INT = (SELECT COUNT(*) FROM dbo.course_allocations);
    DECLARE @cnt_staff          INT = (SELECT COUNT(*) FROM dbo.staff);
    DECLARE @cnt_assignments    INT = (SELECT COUNT(*) FROM dbo.assignments);
    DECLARE @cnt_documents      INT = (SELECT COUNT(*) FROM dbo.documents);
    DECLARE @cnt_submissions    INT = (SELECT COUNT(*) FROM dbo.submissions);

    /* =====================================================================
       1. TABLE dbo.admission_documents
       Why: Persist admission uploads (storage_key, original name, MIME)
            instead of in-memory-only files.
       Introduced: post-Rev 3 file-persistence sprint (security/persistence).
       Entity: AdmissionDocument (Models/AdmissionDocument.cs)
       DbContext: LccCmsDbContext AdmissionDocument fluent map
       Controller: AdmissionsController
         GET/POST /api/admissions
         GET /api/admissions/{id}/documents/{documentId}
       Symptom if missing: Invalid object name 'admission_documents' (error 208)
       ===================================================================== */
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

    IF OBJECT_ID(N'dbo.admission_documents', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_admission_documents_admission'
              AND object_id = OBJECT_ID(N'dbo.admission_documents')
       )
        CREATE INDEX IX_admission_documents_admission
            ON dbo.admission_documents(admission_id);

    /* =====================================================================
       2. TABLE dbo.learning_materials
       Why: Persist lecturer-uploaded course materials per allocation.
       Introduced: post-Rev 3 file-persistence sprint (learning).
       Entity: LearningMaterial (Models/LearningMaterial.cs)
       DbContext: LccCmsDbContext LearningMaterial fluent map
       Controller: LearningController
         GET/POST/DELETE materials, GET material download
       Symptom if missing: Invalid object name 'learning_materials' (error 208)
       Delete behaviour (matches EF):
         allocation FK Restrict (no ON DELETE CASCADE)
         uploaded_by_staff FK ON DELETE SET NULL
       ===================================================================== */
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
        CREATE INDEX IX_learning_materials_allocation
            ON dbo.learning_materials(allocation_id);

    IF OBJECT_ID(N'dbo.learning_materials', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_learning_materials_uploaded_by'
              AND object_id = OBJECT_ID(N'dbo.learning_materials')
       )
        CREATE INDEX IX_learning_materials_uploaded_by
            ON dbo.learning_materials(uploaded_by_staff_id);

    /* =====================================================================
       3. COLUMN assignments.allow_late_submissions
       Why: Lecturers can allow submissions after due_date; submit path
            reads this flag. Existing rows default to 0 (disallowed).
       Introduced: post-Rev 3 learning / assignments sprint.
       Entity: Assignment.AllowLateSubmissions (Models/Assignment.cs)
       Controller: LearningController assignments GET/POST/PUT and submit
       Symptom if missing: Invalid column name 'allow_late_submissions' (207)
       ===================================================================== */
    IF COL_LENGTH(N'dbo.assignments', N'allow_late_submissions') IS NULL
    BEGIN
        ALTER TABLE dbo.assignments ADD allow_late_submissions BIT NOT NULL
            CONSTRAINT DF_assignments_allow_late_submissions DEFAULT (0);
        PRINT N'Added dbo.assignments.allow_late_submissions.';
    END
    ELSE
        PRINT N'Skipped assignments.allow_late_submissions (already exists).';

    /* =====================================================================
       4. COLUMN documents.content_type
       Why: Store MIME type for student documents / profile photo download.
            NULL is valid; API may fall back to application/octet-stream.
       Introduced: post-Rev 3 file-persistence sprint (student photo).
       Entity: Document.ContentType (Models/Document.cs)
       Controller: StudentsController GET/PUT /api/students/me, POST /me/photo
       Symptom if missing: Invalid column name 'content_type' (error 207)
       ===================================================================== */
    IF COL_LENGTH(N'dbo.documents', N'content_type') IS NULL
    BEGIN
        ALTER TABLE dbo.documents ADD content_type NVARCHAR(255) NULL;
        PRINT N'Added dbo.documents.content_type.';
    END
    ELSE
        PRINT N'Skipped documents.content_type (already exists).';

    /* =====================================================================
       5. COLUMN submissions.original_file_name
       Why: Preserve the uploaded file name for downloads. Required in EF.
            Existing rows get N'' via default; optional backfill below.
       Introduced: post-Rev 3 file-persistence sprint (submissions).
       Entity: Submission.OriginalFileName (Models/Submission.cs)
       Controller: LearningController submissions GET/POST/download
       Symptom if missing: Invalid column name 'original_file_name' (207)
       ===================================================================== */
    IF COL_LENGTH(N'dbo.submissions', N'original_file_name') IS NULL
    BEGIN
        ALTER TABLE dbo.submissions ADD original_file_name NVARCHAR(255) NOT NULL
            CONSTRAINT DF_submissions_original_file_name DEFAULT (N'');
        PRINT N'Added dbo.submissions.original_file_name.';
    END
    ELSE
        PRINT N'Skipped submissions.original_file_name (already exists).';

    /* =====================================================================
       6. COLUMN submissions.content_type
       Why: Store MIME type for assignment file downloads. NULL is valid.
       Introduced: post-Rev 3 file-persistence sprint (submissions).
       Entity: Submission.ContentType (Models/Submission.cs)
       Controller: LearningController submissions GET/POST/download
       Symptom if missing: Invalid column name 'content_type' (error 207)
       ===================================================================== */
    IF COL_LENGTH(N'dbo.submissions', N'content_type') IS NULL
    BEGIN
        ALTER TABLE dbo.submissions ADD content_type NVARCHAR(255) NULL;
        PRINT N'Added dbo.submissions.content_type.';
    END
    ELSE
        PRINT N'Skipped submissions.content_type (already exists).';

    /* Optional backfill: copy a filename from file_url when still blank.
       Safe to re-run. Does not change row counts. */
    IF COL_LENGTH(N'dbo.submissions', N'original_file_name') IS NOT NULL
    BEGIN
        UPDATE dbo.submissions
        SET original_file_name =
            RIGHT(file_url, CHARINDEX(N'/', REVERSE(file_url) + N'/') - 1)
        WHERE original_file_name = N''
          AND file_url IS NOT NULL
          AND LEN(file_url) > 0;
    END

    /* Row-count guard: Rev 4 must not insert/delete parent or existing rows. */
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
   B. Post-upgrade verification (run after COMMIT; read-only)
   Expected:
     admission_documents = 1, learning_materials = 1
     four new columns = 1
     new tables row_count = 0 on first upgrade
   ========================================================================= */

SELECT
    N'admission_documents' AS object_name,
    CASE WHEN OBJECT_ID(N'dbo.admission_documents', N'U') IS NOT NULL THEN 1 ELSE 0 END AS exists_flag,
    CASE WHEN OBJECT_ID(N'dbo.admission_documents', N'U') IS NOT NULL
         THEN (SELECT COUNT(*) FROM dbo.admission_documents) ELSE NULL END AS row_count
UNION ALL
SELECT N'learning_materials',
    CASE WHEN OBJECT_ID(N'dbo.learning_materials', N'U') IS NOT NULL THEN 1 ELSE 0 END,
    CASE WHEN OBJECT_ID(N'dbo.learning_materials', N'U') IS NOT NULL
         THEN (SELECT COUNT(*) FROM dbo.learning_materials) ELSE NULL END
UNION ALL
SELECT N'admissions (unchanged parent)', 1, (SELECT COUNT(*) FROM dbo.admissions)
UNION ALL
SELECT N'course_allocations (unchanged parent)', 1, (SELECT COUNT(*) FROM dbo.course_allocations)
UNION ALL
SELECT N'staff (unchanged parent)', 1, (SELECT COUNT(*) FROM dbo.staff)
UNION ALL
SELECT N'assignments (rows preserved)', 1, (SELECT COUNT(*) FROM dbo.assignments)
UNION ALL
SELECT N'documents (rows preserved)', 1, (SELECT COUNT(*) FROM dbo.documents)
UNION ALL
SELECT N'submissions (rows preserved)', 1, (SELECT COUNT(*) FROM dbo.submissions);

SELECT
    N'assignments.allow_late_submissions' AS column_ref,
    CASE WHEN COL_LENGTH(N'dbo.assignments', N'allow_late_submissions') IS NOT NULL THEN 1 ELSE 0 END AS exists_flag
UNION ALL
SELECT N'documents.content_type',
    CASE WHEN COL_LENGTH(N'dbo.documents', N'content_type') IS NOT NULL THEN 1 ELSE 0 END
UNION ALL
SELECT N'submissions.original_file_name',
    CASE WHEN COL_LENGTH(N'dbo.submissions', N'original_file_name') IS NOT NULL THEN 1 ELSE 0 END
UNION ALL
SELECT N'submissions.content_type',
    CASE WHEN COL_LENGTH(N'dbo.submissions', N'content_type') IS NOT NULL THEN 1 ELSE 0 END;

SELECT
    i.name AS index_name,
    OBJECT_NAME(i.object_id) AS table_name
FROM sys.indexes AS i
WHERE i.name IN (
    N'IX_admission_documents_admission',
    N'IX_learning_materials_allocation',
    N'IX_learning_materials_uploaded_by'
)
ORDER BY table_name, index_name;

SELECT
    fk.name AS foreign_key_name,
    OBJECT_NAME(fk.parent_object_id) AS table_name
FROM sys.foreign_keys AS fk
WHERE fk.name IN (
    N'FK__admission_documents__admission',
    N'FK__learning_materials__allocation',
    N'FK__learning_materials__uploaded_by'
)
ORDER BY table_name, foreign_key_name;

GO

/* =========================================================================
   C. Deployment instructions

   1. Confirm the 2026-09-07 LCCCMSDB backup exists and is restorable.
   2. Stop or idle the API so no writes occur during the upgrade.
   3. In SSMS / Azure Data Studio / sqlcmd, connect as a login that can
      ALTER TABLE and CREATE TABLE on LCCCMSDB.
   4. SELECT DB_NAME(); confirm LCCCMSDB (or USE LCCCMSDB).
   5. Execute this entire file. SET XACT_ABORT ON rolls back on error
      before COMMIT. Re-running after a successful COMMIT is a no-op.
   6. Confirm PRINT: "Rev 4 schema upgrade committed."
   7. Confirm verification result sets: exists_flag = 1 for both tables
      and all four columns; parent row counts match the pre-upgrade totals.
   8. Smoke the API (lab X-User-Id as needed):
        GET /api/admissions
        GET /api/students/me
        GET /api/learning/materials
        GET /api/learning/assignments
        GET submissions for an assignment
   9. This file does not replace database/LCC_CMS_Schema.sql. Greenfield
      installs still need Rev 3 + this Rev 4 script until the canonical
      schema file is updated in a later change.

   =========================================================================
   Rollback notes (documentation only — no DROP / DELETE in this file)

   Preferred rollback: restore LCCCMSDB from the 2026-09-07 full backup.
   That returns the database to Rev 3 with all pre-upgrade data.

   Do not DROP admission_documents or learning_materials if the API has
   already written files after upgrade; those rows would be lost and
   storage_key values would become orphaned on disk.

   Do not DROP the four new columns if the API has already written to them;
   EF would throw Invalid column name again, and MIME / file-name data
   would be discarded.

   Column defaults added by this script:
     DF_assignments_allow_late_submissions
     DF_submissions_original_file_name
     DF_admission_documents_uploaded_at
     DF_learning_materials_uploaded_at

   After a backup restore, the current codebase will again require this
   Rev 4 script before admissions, learning materials, assignments,
   submissions, and student photo endpoints are safe to call.
   ========================================================================= */
