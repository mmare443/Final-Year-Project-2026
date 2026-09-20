# Import mock students from "mock student Data.docx". Idempotent on email.
param(
    [string]$DocxPath = "C:\Users\Mond\Desktop\FYP presentations\mock student Data.docx",
    [string]$Server = "localhost",
    [string]$Database = "LCCCMSDB"
)

$ErrorActionPreference = "Stop"
$media = Join-Path $env:TEMP "lcc-mock-students-docx\unzip\word\media"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not (Test-Path (Join-Path $repoRoot "LCC_CMS_Api"))) {
    $repoRoot = "C:\Users\Mond\Documents\Bach Inf0rmatin Systems C0urse\IS 4 Year 4 2026\Final Year Project\System Design\by Claude\lcc-cms"
}

$photoDirs = @(
    (Join-Path $repoRoot "LCC_CMS_Api\LCC_CMS_Api\App_Data\uploads\profiles"),
    (Join-Path $env:TEMP "lcc-cms-api-build-photos\App_Data\uploads\profiles"),
    (Join-Path $env:TEMP "lcc-cms-api-build-print\App_Data\uploads\profiles"),
    (Join-Path $env:TEMP "lcc-cms-api-build-news-r12b\App_Data\uploads\profiles")
)

function Sql-Escape([string]$value) {
    return ($value -replace "'", "''")
}

function Email-FromName([string]$fullName, [System.Collections.Generic.HashSet[string]]$used) {
    $parts = ($fullName.Trim() -split "\s+") | Where-Object { $_ }
    $first = $parts[0].ToLowerInvariant() -replace "[^a-z]", ""
    $last = if ($parts.Count -gt 1) { $parts[-1].ToLowerInvariant() -replace "[^a-z]", "" } else { "student" }
    if (-not $first) { $first = "x" }
    if (-not $last) { $last = "student" }
    $local = $first.Substring(0, 1) + $last
    $email = "$local@student.lccbportal.org"
    $n = 2
    while ($used.Contains($email)) {
        $email = "$local$n@student.lccbportal.org"
        $n++
    }
    [void]$used.Add($email)
    return $email
}

function Normalize-Gender([string]$value) {
    switch -Regex ($value.Trim()) {
        "^(M|Male)$" { return "Male" }
        "^(F|Female)$" { return "Female" }
        default { return $value.Trim() }
    }
}

function Normalize-Year([string]$value) {
    if ($value -match "(\d)") { return [int]$Matches[1] }
    return 1
}

$places = @(
    @{ Province = "Western Highlands"; District = "Mt Hagen"; Village = "Banz" },
    @{ Province = "Jiwaka"; District = "Minj"; Village = "Kudjip" },
    @{ Province = "Chimbu"; District = "Kundiawa"; Village = "Gembogl" },
    @{ Province = "Eastern Highlands"; District = "Goroka"; Village = "Asaro" },
    @{ Province = "Enga"; District = "Wabag"; Village = "Laiagam" },
    @{ Province = "Southern Highlands"; District = "Mendi"; Village = "Kagua" }
)

$students = @(
    @{ Programme = "Diploma in Business Administration and Management"; Name = "Peter Wamp"; Gender = "M"; Year = "1"; Mobile = "72123401"; EmName = "Joseph Wamp"; EmPhone = "71111101"; Photo = "image1.png" },
    @{ Programme = "Diploma in Business Administration and Management"; Name = "Maria Kora"; Gender = "F"; Year = "1"; Mobile = "72123402"; EmName = "Paul Kora"; EmPhone = "71111102"; Photo = "image2.png" },
    @{ Programme = "Diploma in Business Administration and Management"; Name = "Joseph Nali"; Gender = "M"; Year = "2"; Mobile = "72123403"; EmName = "Anna Nali"; EmPhone = "71111103"; Photo = "image3.png" },
    @{ Programme = "Diploma in Business Administration and Management"; Name = "Ruth Yowa"; Gender = "F"; Year = "2"; Mobile = "72123404"; EmName = "John Yowa"; EmPhone = "71111104"; Photo = "image4.png" },
    @{ Programme = "Diploma in Business Administration and Management"; Name = "Michael Wangi"; Gender = "M"; Year = "3"; Mobile = "72123405"; EmName = "Peter Wangi"; EmPhone = "71111105"; Photo = "image5.png" },
    @{ Programme = "Certificate in Business Administration and Management"; Name = "Lucy Imaka"; Gender = "F"; Year = "1"; Mobile = "72123406"; EmName = "David Imaka"; EmPhone = "71111106"; Photo = "image6.png" },
    @{ Programme = "Certificate in Business Administration and Management"; Name = "Stephen Kaupa"; Gender = "M"; Year = "1"; Mobile = "72123407"; EmName = "Mary Kaupa"; EmPhone = "71111107"; Photo = "image7.png" },
    @{ Programme = "Certificate in Business Administration and Management"; Name = "Julie Kepa"; Gender = "F"; Year = "1"; Mobile = "72123408"; EmName = "Thomas Kepa"; EmPhone = "71111108"; Photo = "image8.png" },
    @{ Programme = "Certificate in Business Administration and Management"; Name = "Daniel Posi"; Gender = "M"; Year = "1"; Mobile = "72123409"; EmName = "Susan Posi"; EmPhone = "71111109"; Photo = "image9.png" },
    @{ Programme = "Certificate in Business Administration and Management"; Name = "Rose Kipu"; Gender = "F"; Year = "1"; Mobile = "72123410"; EmName = "Jacob Kipu"; EmPhone = "71111110"; Photo = "image10.png" },
    @{ Programme = "Diploma in Applied Ministry"; Name = "Boundo Clement"; Gender = "Male"; Year = "Year 1"; Mobile = "72123411"; EmName = "Simon Clement"; EmPhone = "71111111"; Photo = "image11.png" },
    @{ Programme = "Diploma in Applied Ministry"; Name = "Sarah Tali"; Gender = "Female"; Year = "Year 1"; Mobile = "72123412"; EmName = "John Tali"; EmPhone = "71111112"; Photo = "image12.png" },
    @{ Programme = "Diploma in Applied Ministry"; Name = "Joel Wapi"; Gender = "Male"; Year = "Year 2"; Mobile = "72123413"; EmName = "Mary Wapi"; EmPhone = "71111113"; Photo = "image13.png" },
    @{ Programme = "Diploma in Applied Ministry"; Name = "Regina Kola"; Gender = "Female"; Year = "Year 2"; Mobile = "72123414"; EmName = "Peter Kola"; EmPhone = "71111114"; Photo = "image14.png" },
    @{ Programme = "Diploma in Applied Ministry"; Name = "Mark Kande"; Gender = "Male"; Year = "Year 3"; Mobile = "72123415"; EmName = "James Kande"; EmPhone = "71111115"; Photo = "image15.png" },
    @{ Programme = "Certificate in Applied Ministry"; Name = "Philip Kuman"; Gender = "M"; Year = "1"; Mobile = "72123416"; EmName = "Ruth Kuman"; EmPhone = "71111116"; Photo = "image16.png" },
    @{ Programme = "Certificate in Applied Ministry"; Name = "Anna Lingi"; Gender = "F"; Year = "1"; Mobile = "72123417"; EmName = "Michael Lingi"; EmPhone = "71111117"; Photo = "image17.png" },
    @{ Programme = "Certificate in Applied Ministry"; Name = "Andrew Konga"; Gender = "M"; Year = "1"; Mobile = "72123418"; EmName = "Sarah Konga"; EmPhone = "71111118"; Photo = "image18.png" },
    @{ Programme = "Certificate in Applied Ministry"; Name = "Mary Yakai"; Gender = "F"; Year = "1"; Mobile = "72123419"; EmName = "Simon Yakai"; EmPhone = "71111119"; Photo = "image19.png" },
    @{ Programme = "Certificate in Applied Ministry"; Name = "Patrick Wai"; Gender = "M"; Year = "1"; Mobile = "72123420"; EmName = "Regina Wai"; EmPhone = "71111120"; Photo = "image20.png" },
    @{ Programme = "Diploma in Tropical Agriculture"; Name = "Jackson Kapi"; Gender = "M"; Year = "1"; Mobile = "72123421"; EmName = "Peter Kapi"; EmPhone = "71111121"; Photo = "image21.png" },
    @{ Programme = "Diploma in Tropical Agriculture"; Name = "Ruth Kuman"; Gender = "F"; Year = "1"; Mobile = "72123422"; EmName = "John Kuman"; EmPhone = "71111122"; Photo = "image22.png" },
    @{ Programme = "Diploma in Tropical Agriculture"; Name = "Isaac Wago"; Gender = "M"; Year = "2"; Mobile = "72123423"; EmName = "Sarah Wago"; EmPhone = "71111123"; Photo = "image23.png" },
    @{ Programme = "Diploma in Tropical Agriculture"; Name = "Marry Lesman"; Gender = "F"; Year = "2"; Mobile = "72123424"; EmName = "Mark Lesman"; EmPhone = "71111124"; Photo = "image24.png" },
    @{ Programme = "Diploma in Tropical Agriculture"; Name = "Benjamin Wau"; Gender = "M"; Year = "3"; Mobile = "72123425"; EmName = "Mary Wau"; EmPhone = "71111125"; Photo = "image25.png" },
    @{ Programme = "Certificate in Tropical Agriculture"; Name = "Steven Roy"; Gender = "M"; Year = "1"; Mobile = "72123426"; EmName = "Paul Roy"; EmPhone = "71111126"; Photo = "image26.png" },
    @{ Programme = "Certificate in Tropical Agriculture"; Name = "Esther Mandi"; Gender = "F"; Year = "1"; Mobile = "72123427"; EmName = "Joseph Mandi"; EmPhone = "71111127"; Photo = "image27.png" },
    @{ Programme = "Certificate in Tropical Agriculture"; Name = "John Kola"; Gender = "M"; Year = "1"; Mobile = "72123428"; EmName = "Ruth Kola"; EmPhone = "71111128"; Photo = "image28.png" },
    @{ Programme = "Certificate in Tropical Agriculture"; Name = "Grace Tona"; Gender = "F"; Year = "1"; Mobile = "72123429"; EmName = "Michael Tona"; EmPhone = "71111129"; Photo = "image29.png" },
    @{ Programme = "Certificate in Tropical Agriculture"; Name = "Lucas Yama"; Gender = "M"; Year = "1"; Mobile = "72123430"; EmName = "Peter Yama"; EmPhone = "71111130"; Photo = "image30.png" }
)

sqlcmd -S $Server -d $Database -E -I -b -i (Join-Path $PSScriptRoot "LCC_CMS_Schema_Upgrade_Rev14_StudentYearLevel.sql") | Out-Host

$imported = @()
$skipped = @()
$usedEmails = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$i = 0
foreach ($s in $students) {
    $email = Email-FromName $s.Name $usedEmails
    $gender = Normalize-Gender $s.Gender
    $year = Normalize-Year $s.Year
    $place = $places[$i % $places.Count]
    $birthYear = 2008 - $year
    $dob = "{0:0000}-{1:00}-{2:00}" -f $birthYear, (($i % 11) + 1), (($i % 27) + 1)
    $postal = "P.O. Box $([int]$s.Mobile.Substring(4)), $($place.District), $($place.Province), Papua New Guinea"
    $photoRel = "uploads/profiles/user-PLACEHOLDER.png"

    $sql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @email NVARCHAR(255) = N'$(Sql-Escape $email)';
DECLARE @name NVARCHAR(150) = N'$(Sql-Escape $s.Name)';
DECLARE @programme NVARCHAR(200) = N'$(Sql-Escape $s.Programme)';
IF EXISTS (SELECT 1 FROM dbo.users WHERE email = @email)
BEGIN
    SELECT N'SKIP' AS action, user_id, email, N'' AS student_number, N'' AS programme_name FROM dbo.users WHERE email = @email;
END
ELSE
BEGIN
DECLARE @programmeId INT = (SELECT TOP(1) programme_id FROM dbo.programmes WHERE programme_name = @programme);
IF @programmeId IS NULL
BEGIN
    SELECT N'SKIP_PROGRAMME' AS action, 0 AS user_id, @email AS email, N'' AS student_number, N'' AS programme_name;
END
ELSE
BEGIN
DECLARE @max INT = 24000;
SELECT @max = ISNULL(MAX(n), 24000)
FROM (
    SELECT TRY_CONVERT(INT, SUBSTRING(student_number, 5, 20)) AS n
    FROM dbo.students
    WHERE student_number LIKE N'LCC-[0-9]%'
) x
WHERE n IS NOT NULL;
DECLARE @studentNumber NVARCHAR(20) = CONCAT(N'LCC-', @max + 1);
DECLARE @entra NVARCHAR(36) = CONVERT(NVARCHAR(36), NEWID());
INSERT INTO dbo.users (entra_id, email, role, status, created_at, activation_used)
VALUES (@entra, @email, N'Student', N'Active', SYSUTCDATETIME(), 0);
DECLARE @userId INT = CONVERT(INT, SCOPE_IDENTITY());
INSERT INTO dbo.students (student_id, student_number, full_name, programme_id, enrolment_status, emergency_contact, postal_address, year_level, province, district, village)
VALUES (
    @userId,
    @studentNumber,
    @name,
    @programmeId,
    N'Enrolled',
    N'$(Sql-Escape $s.EmName)|$(Sql-Escape $s.EmPhone)',
    N'$(Sql-Escape $postal)',
    $year,
    N'$(Sql-Escape $place.Province)',
    N'$(Sql-Escape $place.District)',
    N'$(Sql-Escape $place.Village)'
);
INSERT INTO dbo.admissions (programme_id, applicant_name, applicant_email, applicant_phone, date_of_birth, gender, status, reviewed_by, decision_date, student_id, created_at)
VALUES (
    @programmeId,
    @name,
    @email,
    N'$(Sql-Escape $s.Mobile)',
    N'$dob',
    N'$(Sql-Escape $gender)',
    N'Approved',
    4,
    CAST(SYSUTCDATETIME() AS DATE),
    @userId,
    SYSUTCDATETIME()
);
UPDATE dbo.users
SET profile_photo_url = CONCAT(N'uploads/profiles/user-', @userId, N'.png')
WHERE user_id = @userId;
SELECT N'OK' AS action, @userId AS user_id, @email AS email, @studentNumber AS student_number, @programme AS programme_name;
END
END
"@

    $tmpSql = Join-Path $env:TEMP "lcc-import-one.sql"
    Set-Content -Path $tmpSql -Value $sql -Encoding ascii
    $out = sqlcmd -S $Server -d $Database -E -I -W -s "|" -h-1 -i $tmpSql
    $line = ($out | Where-Object { $_ -and $_.Trim() -ne "" -and $_ -notmatch "rows affected" } | Select-Object -First 1)
    if (-not $line) {
        $skipped += [pscustomobject]@{ Name = $s.Name; Email = $email; Reason = "No SQL result" }
        $i++
        continue
    }
    $parts = $line.Split("|")
    $action = $parts[0].Trim()
    if ($action -eq "SKIP") {
        $skipped += [pscustomobject]@{ Name = $s.Name; Email = $email; Reason = "Email already exists" }
    }
    elseif ($action -eq "SKIP_PROGRAMME") {
        $skipped += [pscustomobject]@{ Name = $s.Name; Email = $email; Reason = "Programme not found: $($s.Programme)" }
    }
    elseif ($action -eq "OK") {
        $userId = [int]$parts[1]
        $row = [pscustomobject]@{
            Name = $s.Name
            Email = $parts[2].Trim()
            StudentNumber = $parts[3].Trim()
            Programme = $parts[4].Trim()
            UserId = $userId
            Photo = $s.Photo
        }
        $src = Join-Path $media $s.Photo
        foreach ($dir in $photoDirs) {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
            if (Test-Path $src) {
                Copy-Item $src (Join-Path $dir "user-$userId.png") -Force
            }
        }
        $imported += $row
    }
    else {
        $skipped += [pscustomobject]@{ Name = $s.Name; Email = $email; Reason = $line }
    }
    $i++
}

Write-Host "IMPORTED=$($imported.Count)"
$imported | Format-Table Name, StudentNumber, Email, Programme, UserId -AutoSize
Write-Host "SKIPPED=$($skipped.Count)"
$skipped | Format-Table -AutoSize
$imported | ConvertTo-Csv -NoTypeInformation | Set-Content (Join-Path $env:TEMP "lcc-imported-students.csv") -Encoding UTF8
