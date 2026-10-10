/*
    Rev 21 — Admission document review and decision email log.

    Additive only. Existing admissions rows, including Approved, are not
    updated or deleted. The users table and password columns are not touched.
    Safe to run more than once.

    This script is not applied to production by the application.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.admission_documents', N'review_outcome') IS NULL
    ALTER TABLE dbo.admission_documents ADD review_outcome NVARCHAR(40) NULL;

IF COL_LENGTH(N'dbo.admission_documents', N'review_remark') IS NULL
    ALTER TABLE dbo.admission_documents ADD review_remark NVARCHAR(1000) NULL;

IF COL_LENGTH(N'dbo.admission_documents', N'reviewed_by') IS NULL
    ALTER TABLE dbo.admission_documents ADD reviewed_by INT NULL;

IF COL_LENGTH(N'dbo.admission_documents', N'reviewed_at') IS NULL
    ALTER TABLE dbo.admission_documents ADD reviewed_at DATETIME2 NULL;

IF COL_LENGTH(N'dbo.admission_documents', N'opened_at') IS NULL
    ALTER TABLE dbo.admission_documents ADD opened_at DATETIME2 NULL;

IF COL_LENGTH(N'dbo.admissions', N'selection_decision') IS NULL
    ALTER TABLE dbo.admissions ADD selection_decision NVARCHAR(80) NULL;

IF COL_LENGTH(N'dbo.admissions', N'decision_remark') IS NULL
    ALTER TABLE dbo.admissions ADD decision_remark NVARCHAR(2000) NULL;

IF COL_LENGTH(N'dbo.admissions', N'decision_override_reason') IS NULL
    ALTER TABLE dbo.admissions ADD decision_override_reason NVARCHAR(1000) NULL;

IF COL_LENGTH(N'dbo.admissions', N'decision_recorded_by') IS NULL
    ALTER TABLE dbo.admissions ADD decision_recorded_by INT NULL;

IF COL_LENGTH(N'dbo.admissions', N'decision_recorded_at') IS NULL
    ALTER TABLE dbo.admissions ADD decision_recorded_at DATETIME2 NULL;

IF OBJECT_ID(N'dbo.admission_email_log', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.admission_email_log (
        admission_email_log_id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_admission_email_log PRIMARY KEY,
        admission_id            INT             NOT NULL,
        decision                NVARCHAR(80)    NOT NULL,
        recipient_email         NVARCHAR(255)   NOT NULL,
        subject                 NVARCHAR(300)   NOT NULL,
        delivery_status         NVARCHAR(20)    NOT NULL,
        attempted_at            DATETIME2       NULL,
        sent_at                 DATETIME2       NULL,
        failure_message         NVARCHAR(500)   NULL,
        initiated_by            INT             NOT NULL,
        CONSTRAINT FK_admission_email_log_admission
            FOREIGN KEY (admission_id) REFERENCES dbo.admissions(admission_id)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_admission_email_log_admission'
      AND object_id = OBJECT_ID(N'dbo.admission_email_log')
)
BEGIN
    CREATE INDEX IX_admission_email_log_admission
        ON dbo.admission_email_log(admission_id, admission_email_log_id);
END

COMMIT TRANSACTION;

/* Verification. These queries do not modify data. */
SELECT
    COL_LENGTH(N'dbo.admission_documents', N'review_outcome') AS review_outcome,
    COL_LENGTH(N'dbo.admission_documents', N'review_remark') AS review_remark,
    COL_LENGTH(N'dbo.admission_documents', N'reviewed_by') AS reviewed_by,
    COL_LENGTH(N'dbo.admission_documents', N'reviewed_at') AS reviewed_at,
    COL_LENGTH(N'dbo.admission_documents', N'opened_at') AS opened_at,
    COL_LENGTH(N'dbo.admissions', N'selection_decision') AS selection_decision,
    COL_LENGTH(N'dbo.admissions', N'decision_remark') AS decision_remark,
    COL_LENGTH(N'dbo.admissions', N'decision_override_reason') AS decision_override_reason,
    COL_LENGTH(N'dbo.admissions', N'decision_recorded_by') AS decision_recorded_by,
    COL_LENGTH(N'dbo.admissions', N'decision_recorded_at') AS decision_recorded_at,
    OBJECT_ID(N'dbo.admission_email_log', N'U') AS email_log_table;

SELECT status, COUNT(*) AS applications
FROM dbo.admissions
GROUP BY status;
