/* Rev 15 — portal login identities on lccbportal.org
   Updates dbo.users.email only (JWT login). Does not change:
     admissions.applicant_email, AzureAd/Graph Domain, contact emails. */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;

/* Staff / institutional portal accounts: domain swap. */
UPDATE dbo.users
SET email = N'registrar@lccbportal.org'
WHERE email = N'registrar@lccb.ac.pg';

UPDATE dbo.users
SET email = N'principal@lccbportal.org'
WHERE email = N'principal@lccb.ac.pg';

UPDATE dbo.users
SET email = N'lecturer@lccbportal.org'
WHERE email = N'lecturer@lccb.ac.pg';

UPDATE dbo.users
SET email = N'hod@lccbportal.org'
WHERE email = N'hod@lccb.ac.pg';

UPDATE dbo.users
SET email = N'admin@lccbportal.org'
WHERE email = N'admin@lccb.ac.pg';

UPDATE dbo.users
SET email = N'hod.ministry@lccbportal.org'
WHERE email = N'hod.ministry@lccb.ac.pg';

UPDATE dbo.users
SET email = N'hod.agriculture@lccbportal.org'
WHERE email = N'hod.agriculture@lccb.ac.pg';

UPDATE dbo.users
SET email = N'lecturer.kila@lccbportal.org'
WHERE email = N'lecturer.kila@lccb.ac.pg';

UPDATE dbo.users
SET email = N'lecturer.ministry@lccbportal.org'
WHERE email = N'lecturer.ministry@lccb.ac.pg';

UPDATE dbo.users
SET email = N'lecturer.agriculture@lccbportal.org'
WHERE email = N'lecturer.agriculture@lccb.ac.pg';

/* Remaining staff/management logins still on the old academic domain. */
UPDATE dbo.users
SET email = REPLACE(email, N'@lccb.ac.pg', N'@lccbportal.org')
WHERE email LIKE N'%@lccb.ac.pg'
  AND role <> N'Student';

/* Existing lab student LCC26001 */
UPDATE dbo.users
SET email = N'kstudent@student.lccbportal.org'
WHERE email IN (N'student@lccb.ac.pg', N'student@lccbportal.org')
  AND user_id IN (SELECT student_id FROM dbo.students WHERE student_number = N'LCC26001');

/* Imported demo students LCC-24001..24030 — initial + surname @student.lccbportal.org */
UPDATE dbo.users SET email = N'pwamp@student.lccbportal.org'   WHERE email = N'peter.wamp@student.lccbportal.org';
UPDATE dbo.users SET email = N'mkora@student.lccbportal.org'   WHERE email = N'maria.kora@student.lccbportal.org';
UPDATE dbo.users SET email = N'jnali@student.lccbportal.org'   WHERE email = N'joseph.nali@student.lccbportal.org';
UPDATE dbo.users SET email = N'ryowa@student.lccbportal.org'   WHERE email = N'ruth.yowa@student.lccbportal.org';
UPDATE dbo.users SET email = N'mwangi@student.lccbportal.org'  WHERE email = N'michael.wangi@student.lccbportal.org';
UPDATE dbo.users SET email = N'limaka@student.lccbportal.org'  WHERE email = N'lucy.imaka@student.lccbportal.org';
UPDATE dbo.users SET email = N'skaupa@student.lccbportal.org'  WHERE email = N'stephen.kaupa@student.lccbportal.org';
UPDATE dbo.users SET email = N'jkepa@student.lccbportal.org'   WHERE email = N'julie.kepa@student.lccbportal.org';
UPDATE dbo.users SET email = N'dposi@student.lccbportal.org'   WHERE email = N'daniel.posi@student.lccbportal.org';
UPDATE dbo.users SET email = N'rkipu@student.lccbportal.org'   WHERE email = N'rose.kipu@student.lccbportal.org';
UPDATE dbo.users SET email = N'bclement@student.lccbportal.org' WHERE email = N'boundo.clement@student.lccbportal.org';
UPDATE dbo.users SET email = N'stali@student.lccbportal.org'   WHERE email = N'sarah.tali@student.lccbportal.org';
UPDATE dbo.users SET email = N'jwapi@student.lccbportal.org'   WHERE email = N'joel.wapi@student.lccbportal.org';
UPDATE dbo.users SET email = N'rkola@student.lccbportal.org'   WHERE email = N'regina.kola@student.lccbportal.org';
UPDATE dbo.users SET email = N'mkande@student.lccbportal.org'  WHERE email = N'mark.kande@student.lccbportal.org';
UPDATE dbo.users SET email = N'pkuman@student.lccbportal.org'  WHERE email = N'philip.kuman@student.lccbportal.org';
UPDATE dbo.users SET email = N'alingi@student.lccbportal.org'  WHERE email = N'anna.lingi@student.lccbportal.org';
UPDATE dbo.users SET email = N'akonga@student.lccbportal.org'  WHERE email = N'andrew.konga@student.lccbportal.org';
UPDATE dbo.users SET email = N'myakai@student.lccbportal.org'  WHERE email = N'mary.yakai@student.lccbportal.org';
UPDATE dbo.users SET email = N'pwai@student.lccbportal.org'    WHERE email = N'patrick.wai@student.lccbportal.org';
UPDATE dbo.users SET email = N'jkapi@student.lccbportal.org'   WHERE email = N'jackson.kapi@student.lccbportal.org';
UPDATE dbo.users SET email = N'rkuman@student.lccbportal.org'  WHERE email = N'ruth.kuman@student.lccbportal.org';
UPDATE dbo.users SET email = N'iwago@student.lccbportal.org'   WHERE email = N'isaac.wago@student.lccbportal.org';
UPDATE dbo.users SET email = N'mlesman@student.lccbportal.org' WHERE email = N'marry.lesman@student.lccbportal.org';
UPDATE dbo.users SET email = N'bwau@student.lccbportal.org'    WHERE email = N'benjamin.wau@student.lccbportal.org';
UPDATE dbo.users SET email = N'sroy@student.lccbportal.org'    WHERE email = N'steven.roy@student.lccbportal.org';
UPDATE dbo.users SET email = N'emandi@student.lccbportal.org'  WHERE email = N'esther.mandi@student.lccbportal.org';
UPDATE dbo.users SET email = N'jkola@student.lccbportal.org'   WHERE email = N'john.kola@student.lccbportal.org';
UPDATE dbo.users SET email = N'gtona@student.lccbportal.org'   WHERE email = N'grace.tona@student.lccbportal.org';
UPDATE dbo.users SET email = N'lyama@student.lccbportal.org'   WHERE email = N'lucas.yama@student.lccbportal.org';

COMMIT TRANSACTION;

SELECT email, COUNT(*) AS n
FROM dbo.users
GROUP BY email
HAVING COUNT(*) > 1;

SELECT user_id, email, role
FROM dbo.users
WHERE email LIKE N'%@lccb.ac.pg'
ORDER BY email;
