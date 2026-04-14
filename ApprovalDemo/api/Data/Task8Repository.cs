using ApprovalDemo.Api.Models;
using Microsoft.Data.SqlClient;
using Npgsql;
using System.Data;

namespace ApprovalDemo.Api.Data;

public sealed class Task8Repository
{
    private readonly string _connectionString;
    private readonly string? _mssqlConnectionString;

    private static readonly IReadOnlyDictionary<int, string> ReportTitles = new Dictionary<int, string>
    {
        [1] = "Active number of students",
        [2] = "Active programs",
        [3] = "Program-wise student count (active students)",
        [4] = "Academic terms (current school year)",
        [5] = "Staff totals by category",
        [6] = "IT Software team members",
        [7] = "Active students — full name, grade, section, academic year",
        [8] = "Active grades and sections with HRT / mentor",
        [9] = "Nucleus system accounts",
        [10] = "HRIS leave balance"
    };

    public Task8Repository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _mssqlConnectionString = configuration.GetConnectionString("MssqlReporting");
    }

    public async Task EnsureSchemaAndSeedAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string ddl = @"
CREATE TABLE IF NOT EXISTS ""AcademicProgram"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""Code"" VARCHAR(50) NOT NULL UNIQUE,
    ""Name"" VARCHAR(200) NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS ""AcademicTerm"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""TermCode"" VARCHAR(50) NOT NULL,
    ""TermName"" VARCHAR(200) NOT NULL,
    ""SchoolYearCode"" VARCHAR(20) NOT NULL,
    ""StartDate"" DATE NOT NULL,
    ""EndDate"" DATE NOT NULL,
    ""IsInCurrentSchoolYear"" BOOLEAN NOT NULL DEFAULT FALSE,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (""TermCode"", ""SchoolYearCode"")
);

CREATE TABLE IF NOT EXISTS ""GradeSectionMentor"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""GradeName"" VARCHAR(80) NOT NULL,
    ""SectionName"" VARCHAR(80) NOT NULL,
    ""MentorStaffCode"" VARCHAR(50) NOT NULL,
    ""MentorFullName"" VARCHAR(200) NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (""GradeName"", ""SectionName"")
);

CREATE TABLE IF NOT EXISTS ""HrisLeaveBalance"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""StaffCode"" VARCHAR(50) NOT NULL,
    ""LeaveType"" VARCHAR(80) NOT NULL,
    ""BalanceDays"" NUMERIC(6,2) NOT NULL,
    ""AsOfDate"" DATE NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (""StaffCode"", ""LeaveType"", ""AsOfDate"")
);

ALTER TABLE ""StudentDirectory"" ADD COLUMN IF NOT EXISTS ""ProgramCode"" VARCHAR(50) NULL;
ALTER TABLE ""StudentDirectory"" ADD COLUMN IF NOT EXISTS ""AcademicYearCode"" VARCHAR(20) NOT NULL DEFAULT '2025-26';

ALTER TABLE ""StaffDirectory"" ADD COLUMN IF NOT EXISTS ""StaffCategory"" VARCHAR(30) NOT NULL DEFAULT 'Academic';
";

        await using (var cmd = new NpgsqlCommand(ddl, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string seedPrograms = @"
INSERT INTO ""AcademicProgram"" (""Code"", ""Name"", ""IsActive"")
VALUES
('PRG-DP', 'Diploma Programme', TRUE),
('PRG-MYP', 'Middle Years Programme', TRUE),
('PRG-PYP', 'Primary Years Programme', TRUE),
('PRG-HS', 'High School Pathway', FALSE)
ON CONFLICT (""Code"") DO NOTHING;

INSERT INTO ""AcademicTerm"" (""TermCode"", ""TermName"", ""SchoolYearCode"", ""StartDate"", ""EndDate"", ""IsInCurrentSchoolYear"")
VALUES
('T1', 'Term 1', '2025-26', DATE '2025-08-01', DATE '2025-12-20', TRUE),
('T2', 'Term 2', '2025-26', DATE '2026-01-06', DATE '2026-04-15', TRUE),
('T3', 'Term 3', '2025-26', DATE '2026-04-16', DATE '2026-06-30', TRUE),
('T1-PRIOR', 'Term 1', '2024-25', DATE '2024-08-01', DATE '2024-12-20', FALSE)
ON CONFLICT (""TermCode"", ""SchoolYearCode"") DO NOTHING;

INSERT INTO ""GradeSectionMentor"" (""GradeName"", ""SectionName"", ""MentorStaffCode"", ""MentorFullName"")
VALUES
('Grade 5', 'Mavericks - 5', 'STF-1003', 'Lulua Bandookwala'),
('Grade 5', 'Collaboration', 'STF-1001', 'Lavesh Paryani'),
('Grade 6', 'G6', 'STF-1002', 'Lubaina Faizullabhai'),
('Grade 7', 'G7', 'STF-1001', 'Lavesh Paryani'),
('Grade 8', 'G8', 'STF-1002', 'Lubaina Faizullabhai'),
('Grade 9', 'G9', 'STF-1004', 'Maheen Khan'),
('Grade 10', 'G10', 'STF-1005', 'Mahek Kothari'),
('Grade 11', 'Mavericks - 11', 'STF-1004', 'Maheen Khan'),
('Grade 12', 'Mavericks - 12', 'STF-1005', 'Mahek Kothari'),
('Grade 12', 'Sangfroid', 'STF-1005', 'Mahek Kothari'),
('Grade 11', 'Aplomb', 'STF-1004', 'Maheen Khan'),
('Grade 12', 'Equanimity', 'STF-1005', 'Mahek Kothari')
ON CONFLICT (""GradeName"", ""SectionName"") DO NOTHING;

INSERT INTO ""HrisLeaveBalance"" (""StaffCode"", ""LeaveType"", ""BalanceDays"", ""AsOfDate"")
VALUES
('STF-1001', 'Annual', 18.5, CURRENT_DATE),
('STF-1001', 'Sick', 6.0, CURRENT_DATE),
('STF-1007', 'Annual', 12.0, CURRENT_DATE),
('STF-1008', 'Annual', 14.0, CURRENT_DATE),
('STF-1011', 'Annual', 20.0, CURRENT_DATE)
ON CONFLICT (""StaffCode"", ""LeaveType"", ""AsOfDate"") DO NOTHING;
";

        await using (var seedCmd = new NpgsqlCommand(seedPrograms, connection))
        {
            await seedCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string backfill = @"
UPDATE ""StudentDirectory"" SET ""AcademicYearCode"" = '2025-26' WHERE ""AcademicYearCode"" IS NULL OR TRIM(""AcademicYearCode"") = '';

UPDATE ""StudentDirectory"" s SET ""ProgramCode"" = v.""Code""
FROM (VALUES
  ('FSK2019011', 'PRG-PYP'),
  ('FSK2023002', 'PRG-DP'),
  ('FSK2025303', 'PRG-DP'),
  ('FSK2023201', 'PRG-MYP'),
  ('FSK2025004', 'PRG-DP'),
  ('FSK2023001', 'PRG-MYP'),
  ('FSK2019001', 'PRG-DP')
) AS m(code, prog)
WHERE s.""StudentCode"" = m.code;

UPDATE ""StudentDirectory"" SET ""ProgramCode"" = 'PRG-MYP' WHERE ""ProgramCode"" IS NULL AND ""GradeName"" IN ('Grade 6','Grade 7','Grade 8');
UPDATE ""StudentDirectory"" SET ""ProgramCode"" = 'PRG-PYP' WHERE ""ProgramCode"" IS NULL AND ""GradeName"" IN ('Grade 1','Grade 2','Grade 3','Grade 4','Grade 5');
UPDATE ""StudentDirectory"" SET ""ProgramCode"" = 'PRG-DP' WHERE ""ProgramCode"" IS NULL AND ""GradeName"" IN ('Grade 11','Grade 12');
UPDATE ""StudentDirectory"" SET ""ProgramCode"" = 'PRG-MYP' WHERE ""ProgramCode"" IS NULL;

INSERT INTO ""StaffDirectory"" (""StaffCode"", ""FullName"", ""DepartmentName"", ""TeamName"", ""Designation"", ""PhotoUrl"", ""IsActive"", ""IsSystemAccount"", ""StaffCategory"")
VALUES
('STF-1101', 'Neha Kulkarni', 'Technology', 'IT Software', 'Software Engineer', 'https://i.pravatar.cc/120?img=81', TRUE, FALSE, 'Support'),
('STF-1102', 'Karan Deshpande', 'Technology', 'IT Software', 'DevOps Engineer', 'https://i.pravatar.cc/120?img=82', TRUE, FALSE, 'Support'),
('STF-1103', 'SYSTEM-NODE-A', 'System', 'Nucleus', 'Integration Bot', NULL, TRUE, TRUE, 'Support')
ON CONFLICT (""StaffCode"") DO NOTHING;

UPDATE ""StaffDirectory"" SET ""TeamName"" = 'IT Software', ""DepartmentName"" = 'Technology', ""StaffCategory"" = 'Support'
WHERE ""StaffCode"" IN ('STF-1007', 'STF-1008');

UPDATE ""StaffDirectory"" SET ""StaffCategory"" = 'Admin' WHERE ""DepartmentName"" = 'Administration';
UPDATE ""StaffDirectory"" SET ""StaffCategory"" = 'Support' WHERE ""DepartmentName"" IN ('Technology','Operations','System');
UPDATE ""StaffDirectory"" SET ""StaffCategory"" = 'Academic' WHERE ""DepartmentName"" = 'Academics';
";

        await using (var bf = new NpgsqlCommand(backfill, connection))
        {
            await bf.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<Task8ReportResponse> GetReportAsync(
        int reportId,
        int page,
        int pageSize,
        string? search,
        bool useMssqlMirror,
        CancellationToken cancellationToken)
    {
        var title = ReportTitles[reportId];
        var norm = Normalize(search);

        if (reportId == 7 && useMssqlMirror)
        {
            if (string.IsNullOrWhiteSpace(_mssqlConnectionString))
            {
                throw new InvalidOperationException("MssqlReporting is not configured; cannot read from mirror.");
            }

            return await GetReport7FromMssqlAsync(page, pageSize, norm, title, cancellationToken);
        }

        return reportId switch
        {
            1 => await GetReport1Async(page, pageSize, norm, title, cancellationToken),
            2 => await GetReport2Async(page, pageSize, norm, title, cancellationToken),
            3 => await GetReport3Async(page, pageSize, norm, title, cancellationToken),
            4 => await GetReport4Async(page, pageSize, norm, title, cancellationToken),
            5 => await GetReport5Async(page, pageSize, norm, title, cancellationToken),
            6 => await GetReport6Async(page, pageSize, norm, title, cancellationToken),
            7 => await GetReport7PostgresAsync(page, pageSize, norm, title, cancellationToken),
            8 => await GetReport8Async(page, pageSize, norm, title, cancellationToken),
            9 => await GetReport9Async(page, pageSize, norm, title, cancellationToken),
            10 => await GetReport10Async(page, pageSize, norm, title, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(reportId))
        };
    }

    private async Task<Task8ReportResponse> GetReport1Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"SELECT COUNT(*) FROM ""StudentDirectory"" WHERE COALESCE(""IsActive"", TRUE) = TRUE";
        await using var cmd = new NpgsqlCommand(sql, connection);
        var countObj = await cmd.ExecuteScalarAsync(cancellationToken);
        var active = Convert.ToInt32(countObj ?? 0);

        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Metric"] = "Active students",
            ["Count"] = active.ToString()
        };

        var all = new List<Dictionary<string, string>> { row };
        var filtered = FilterRowsBySearch(all, norm, ["Metric", "Count"]);
        return SlicePage(filtered, page, pageSize, 1, title, columns: ["Metric", "Count"]);
    }

    private async Task<Task8ReportResponse> GetReport2Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""AcademicProgram""
WHERE ""IsActive"" = TRUE
  AND (@Search IS NULL OR ""Code"" ILIKE '%' || @Search || '%' OR ""Name"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT ""Code"", ""Name""
FROM ""AcademicProgram""
WHERE ""IsActive"" = TRUE
  AND (@Search IS NULL OR ""Code"" ILIKE '%' || @Search || '%' OR ""Name"" ILIKE '%' || @Search || '%')
ORDER BY ""Name""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 2, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Code"] = reader.GetString(reader.GetOrdinal("Code")),
                    ["Name"] = reader.GetString(reader.GetOrdinal("Name"))
                };
                return d;
            },
            ["Code", "Name"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport3Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM (
 SELECT p.""Id""
    FROM ""AcademicProgram"" p
    LEFT JOIN ""StudentDirectory"" s ON s.""ProgramCode"" = p.""Code"" AND COALESCE(s.""IsActive"", TRUE) = TRUE
    WHERE p.""IsActive"" = TRUE
    GROUP BY p.""Id"", p.""Code"", p.""Name""
    HAVING (@Search IS NULL OR p.""Code"" ILIKE '%' || @Search || '%' OR p.""Name"" ILIKE '%' || @Search || '%'
        OR CAST(COUNT(s.""Id"") AS TEXT) ILIKE '%' || @Search || '%')
) t;";

        const string listSql = @"
SELECT p.""Code"", p.""Name"", COUNT(s.""Id"")::BIGINT AS ""ActiveStudents""
FROM ""AcademicProgram"" p
LEFT JOIN ""StudentDirectory"" s ON s.""ProgramCode"" = p.""Code"" AND COALESCE(s.""IsActive"", TRUE) = TRUE
WHERE p.""IsActive"" = TRUE
GROUP BY p.""Id"", p.""Code"", p.""Name""
HAVING (@Search IS NULL OR p.""Code"" ILIKE '%' || @Search || '%' OR p.""Name"" ILIKE '%' || @Search || '%'
    OR CAST(COUNT(s.""Id"") AS TEXT) ILIKE '%' || @Search || '%')
ORDER BY p.""Name""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 3, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ProgramCode"] = reader.GetString(reader.GetOrdinal("Code")),
                    ["ProgramName"] = reader.GetString(reader.GetOrdinal("Name")),
                    ["ActiveStudents"] = reader.GetInt64(reader.GetOrdinal("ActiveStudents")).ToString()
                };
                return d;
            },
            ["ProgramCode", "ProgramName", "ActiveStudents"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport4Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""AcademicTerm""
WHERE ""IsInCurrentSchoolYear"" = TRUE
  AND (@Search IS NULL OR ""TermCode"" ILIKE '%' || @Search || '%' OR ""TermName"" ILIKE '%' || @Search || '%'
    OR ""SchoolYearCode"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT ""TermCode"", ""TermName"", ""SchoolYearCode"", ""StartDate"", ""EndDate""
FROM ""AcademicTerm""
WHERE ""IsInCurrentSchoolYear"" = TRUE
  AND (@Search IS NULL OR ""TermCode"" ILIKE '%' || @Search || '%' OR ""TermName"" ILIKE '%' || @Search || '%'
    OR ""SchoolYearCode"" ILIKE '%' || @Search || '%')
ORDER BY ""StartDate""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 4, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TermCode"] = reader.GetString(reader.GetOrdinal("TermCode")),
                    ["TermName"] = reader.GetString(reader.GetOrdinal("TermName")),
                    ["SchoolYear"] = reader.GetString(reader.GetOrdinal("SchoolYearCode")),
                    ["StartDate"] = reader.GetDateTime(reader.GetOrdinal("StartDate")).ToString("yyyy-MM-dd"),
                    ["EndDate"] = reader.GetDateTime(reader.GetOrdinal("EndDate")).ToString("yyyy-MM-dd")
                };
                return d;
            },
            ["TermCode", "TermName", "SchoolYear", "StartDate", "EndDate"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport5Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM (
    SELECT ""StaffCategory""
    FROM ""StaffDirectory""
    WHERE COALESCE(""IsActive"", TRUE) = TRUE AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
    GROUP BY ""StaffCategory""
    HAVING (@Search IS NULL OR ""StaffCategory"" ILIKE '%' || @Search || '%'
        OR CAST(COUNT(*) AS TEXT) ILIKE '%' || @Search || '%')
) t;";

        const string listSql = @"
SELECT ""StaffCategory"", COUNT(*)::BIGINT AS ""Headcount""
FROM ""StaffDirectory""
WHERE COALESCE(""IsActive"", TRUE) = TRUE AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
GROUP BY ""StaffCategory""
HAVING (@Search IS NULL OR ""StaffCategory"" ILIKE '%' || @Search || '%'
    OR CAST(COUNT(*) AS TEXT) ILIKE '%' || @Search || '%')
ORDER BY ""StaffCategory""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 5, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StaffCategory"] = reader.GetString(reader.GetOrdinal("StaffCategory")),
                    ["Headcount"] = reader.GetInt64(reader.GetOrdinal("Headcount")).ToString()
                };
                return d;
            },
            ["StaffCategory", "Headcount"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport6Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""StaffDirectory""
WHERE ""TeamName"" = 'IT Software'
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
  AND (@Search IS NULL OR ""StaffCode"" ILIKE '%' || @Search || '%' OR ""FullName"" ILIKE '%' || @Search || '%'
    OR ""Designation"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT ""StaffCode"", ""FullName"", ""Designation""
FROM ""StaffDirectory""
WHERE ""TeamName"" = 'IT Software'
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND COALESCE(""IsSystemAccount"", FALSE) = FALSE
  AND (@Search IS NULL OR ""StaffCode"" ILIKE '%' || @Search || '%' OR ""FullName"" ILIKE '%' || @Search || '%'
    OR ""Designation"" ILIKE '%' || @Search || '%')
ORDER BY ""FullName""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 6, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StaffCode"] = reader.GetString(reader.GetOrdinal("StaffCode")),
                    ["FullName"] = reader.GetString(reader.GetOrdinal("FullName")),
                    ["Designation"] = reader.GetString(reader.GetOrdinal("Designation"))
                };
                return d;
            },
            ["StaffCode", "FullName", "Designation"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport7PostgresAsync(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""StudentDirectory""
WHERE COALESCE(""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR ""FullName"" ILIKE '%' || @Search || '%' OR ""GradeName"" ILIKE '%' || @Search || '%'
    OR ""SectionName"" ILIKE '%' || @Search || '%' OR ""AcademicYearCode"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT ""FullName"", ""GradeName"", ""SectionName"", ""AcademicYearCode""
FROM ""StudentDirectory""
WHERE COALESCE(""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR ""FullName"" ILIKE '%' || @Search || '%' OR ""GradeName"" ILIKE '%' || @Search || '%'
    OR ""SectionName"" ILIKE '%' || @Search || '%' OR ""AcademicYearCode"" ILIKE '%' || @Search || '%')
ORDER BY ""FullName""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 7, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["FullName"] = reader.GetString(reader.GetOrdinal("FullName")),
                    ["Grade"] = reader.GetString(reader.GetOrdinal("GradeName")),
                    ["Section"] = reader.GetString(reader.GetOrdinal("SectionName")),
                    ["AcademicYear"] = reader.GetString(reader.GetOrdinal("AcademicYearCode"))
                };
                return d;
            },
            ["FullName", "Grade", "Section", "AcademicYear"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport7FromMssqlAsync(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_mssqlConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM dbo.StudentDirectoryMirror
WHERE IsActive = 1 AND (FullName LIKE @Like OR GradeName LIKE @Like OR SectionName LIKE @Like OR AcademicYearCode LIKE @Like);";

        const string listSql = @"
SELECT FullName, GradeName, SectionName, AcademicYearCode
FROM dbo.StudentDirectoryMirror
WHERE IsActive = 1
  AND (FullName LIKE @Like OR GradeName LIKE @Like OR SectionName LIKE @Like OR AcademicYearCode LIKE @Like)
ORDER BY FullName
OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;";

        await using var countCmd = new SqlCommand(countSql, connection);
        AddMssqlSearchParameters(countCmd, norm);
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

        var rows = new List<Dictionary<string, string>>();
        await using var listCmd = new SqlCommand(listSql, connection);
        AddMssqlSearchParameters(listCmd, norm);
        listCmd.Parameters.Add("@Offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        listCmd.Parameters.Add("@Limit", SqlDbType.Int).Value = pageSize;

        await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["FullName"] = reader.GetString(reader.GetOrdinal("FullName")),
                ["Grade"] = reader.GetString(reader.GetOrdinal("GradeName")),
                ["Section"] = reader.GetString(reader.GetOrdinal("SectionName")),
                ["AcademicYear"] = reader.GetString(reader.GetOrdinal("AcademicYearCode"))
            });
        }

        return new Task8ReportResponse
        {
            ReportId = 7,
            Title = title,
            Columns = ["FullName", "Grade", "Section", "AcademicYear"],
            Rows = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            DataSourceNote = "SQL Server mirror (dbo.StudentDirectoryMirror); aligns with dbo.sp_Task8_ActiveStudentsDetail result set."
        };
    }

    private static void AddMssqlSearchParameters(SqlCommand command, string? norm)
    {
        var like = string.IsNullOrEmpty(norm) ? "%" : "%" + norm + "%";
        command.Parameters.Add("@Like", SqlDbType.NVarChar, 400).Value = like;
    }

    private async Task<Task8ReportResponse> GetReport8Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""GradeSectionMentor"" g
WHERE EXISTS (
    SELECT 1 FROM ""StudentDirectory"" s
    WHERE COALESCE(s.""IsActive"", TRUE) = TRUE
      AND s.""GradeName"" = g.""GradeName"" AND s.""SectionName"" = g.""SectionName"")
  AND (@Search IS NULL OR g.""GradeName"" ILIKE '%' || @Search || '%' OR g.""SectionName"" ILIKE '%' || @Search || '%'
    OR g.""MentorStaffCode"" ILIKE '%' || @Search || '%' OR g.""MentorFullName"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT g.""GradeName"", g.""SectionName"", g.""MentorStaffCode"", g.""MentorFullName""
FROM ""GradeSectionMentor"" g
WHERE EXISTS (
    SELECT 1 FROM ""StudentDirectory"" s
    WHERE COALESCE(s.""IsActive"", TRUE) = TRUE
      AND s.""GradeName"" = g.""GradeName"" AND s.""SectionName"" = g.""SectionName"")
  AND (@Search IS NULL OR g.""GradeName"" ILIKE '%' || @Search || '%' OR g.""SectionName"" ILIKE '%' || @Search || '%'
    OR g.""MentorStaffCode"" ILIKE '%' || @Search || '%' OR g.""MentorFullName"" ILIKE '%' || @Search || '%')
ORDER BY g.""GradeName"", g.""SectionName""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 8, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Grade"] = reader.GetString(reader.GetOrdinal("GradeName")),
                    ["Section"] = reader.GetString(reader.GetOrdinal("SectionName")),
                    ["MentorStaffCode"] = reader.GetString(reader.GetOrdinal("MentorStaffCode")),
                    ["MentorName"] = reader.GetString(reader.GetOrdinal("MentorFullName"))
                };
                return d;
            },
            ["Grade", "Section", "MentorStaffCode", "MentorName"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport9Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""StaffDirectory""
WHERE COALESCE(""IsSystemAccount"", FALSE) = TRUE
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR ""StaffCode"" ILIKE '%' || @Search || '%' OR ""FullName"" ILIKE '%' || @Search || '%'
    OR ""DepartmentName"" ILIKE '%' || @Search || '%' OR ""TeamName"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT ""StaffCode"", ""FullName"", ""DepartmentName"", ""TeamName"", ""Designation""
FROM ""StaffDirectory""
WHERE COALESCE(""IsSystemAccount"", FALSE) = TRUE
  AND COALESCE(""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR ""StaffCode"" ILIKE '%' || @Search || '%' OR ""FullName"" ILIKE '%' || @Search || '%'
    OR ""DepartmentName"" ILIKE '%' || @Search || '%' OR ""TeamName"" ILIKE '%' || @Search || '%')
ORDER BY ""FullName""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 9, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StaffCode"] = reader.GetString(reader.GetOrdinal("StaffCode")),
                    ["FullName"] = reader.GetString(reader.GetOrdinal("FullName")),
                    ["Department"] = reader.GetString(reader.GetOrdinal("DepartmentName")),
                    ["Team"] = reader.GetString(reader.GetOrdinal("TeamName")),
                    ["Designation"] = reader.GetString(reader.GetOrdinal("Designation"))
                };
                return d;
            },
            ["StaffCode", "FullName", "Department", "Team", "Designation"],
            cancellationToken);
    }

    private async Task<Task8ReportResponse> GetReport10Async(int page, int pageSize, string? norm, string title, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = @"
SELECT COUNT(*) FROM ""HrisLeaveBalance"" h
INNER JOIN ""StaffDirectory"" s ON s.""StaffCode"" = h.""StaffCode""
WHERE COALESCE(s.""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR h.""StaffCode"" ILIKE '%' || @Search || '%' OR s.""FullName"" ILIKE '%' || @Search || '%'
    OR h.""LeaveType"" ILIKE '%' || @Search || '%');";

        const string listSql = @"
SELECT h.""StaffCode"", s.""FullName"", h.""LeaveType"", h.""BalanceDays"", h.""AsOfDate""
FROM ""HrisLeaveBalance"" h
INNER JOIN ""StaffDirectory"" s ON s.""StaffCode"" = h.""StaffCode""
WHERE COALESCE(s.""IsActive"", TRUE) = TRUE
  AND (@Search IS NULL OR h.""StaffCode"" ILIKE '%' || @Search || '%' OR s.""FullName"" ILIKE '%' || @Search || '%'
    OR h.""LeaveType"" ILIKE '%' || @Search || '%')
ORDER BY s.""FullName"", h.""LeaveType""
LIMIT @Limit OFFSET @Offset;";

        return await QueryPagedAsync(connection, countSql, listSql, page, pageSize, norm, 10, title,
            reader =>
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["StaffCode"] = reader.GetString(reader.GetOrdinal("StaffCode")),
                    ["FullName"] = reader.GetString(reader.GetOrdinal("FullName")),
                    ["LeaveType"] = reader.GetString(reader.GetOrdinal("LeaveType")),
                    ["BalanceDays"] = reader.GetDecimal(reader.GetOrdinal("BalanceDays")).ToString("0.##"),
                    ["AsOfDate"] = reader.GetDateTime(reader.GetOrdinal("AsOfDate")).ToString("yyyy-MM-dd")
                };
                return d;
            },
            ["StaffCode", "FullName", "LeaveType", "BalanceDays", "AsOfDate"],
            cancellationToken);
    }

    private static Task8ReportResponse SlicePage(
        IReadOnlyList<Dictionary<string, string>> filtered,
        int page,
        int pageSize,
        int reportId,
        string title,
        IReadOnlyList<string> columns)
    {
        var total = filtered.Count;
        var skip = (page - 1) * pageSize;
        var pageRows = filtered.Skip(skip).Take(pageSize).Select(r => (IReadOnlyDictionary<string, string>)r).ToList();
        return new Task8ReportResponse
        {
            ReportId = reportId,
            Title = title,
            Columns = columns,
            Rows = pageRows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static List<Dictionary<string, string>> FilterRowsBySearch(
        IReadOnlyList<Dictionary<string, string>> rows,
        string? norm,
        IReadOnlyList<string> columnKeys)
    {
        if (string.IsNullOrEmpty(norm))
        {
            return rows.ToList();
        }

        var n = norm.ToLowerInvariant();
        return rows
            .Where(r => columnKeys.Any(k => r.TryGetValue(k, out var v) && v.ToLowerInvariant().Contains(n)))
            .ToList();
    }

    private static async Task<Task8ReportResponse> QueryPagedAsync(
        NpgsqlConnection connection,
        string countSql,
        string listSql,
        int page,
        int pageSize,
        string? norm,
        int reportId,
        string title,
        Func<NpgsqlDataReader, Dictionary<string, string>> mapRow,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        await using var countCmd = new NpgsqlCommand(countSql, connection);
        countCmd.Parameters.Add("@Search", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)norm ?? DBNull.Value;
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));

        var rows = new List<Dictionary<string, string>>();
        await using var listCmd = new NpgsqlCommand(listSql, connection);
        listCmd.Parameters.Add("@Search", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)norm ?? DBNull.Value;
        listCmd.Parameters.AddWithValue("@Limit", pageSize);
        listCmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);

        await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(mapRow(reader));
        }

        return new Task8ReportResponse
        {
            ReportId = reportId,
            Title = title,
            Columns = columns,
            Rows = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static string? Normalize(string? search)
    {
        return string.IsNullOrWhiteSpace(search) ? null : search.Trim();
    }
}
