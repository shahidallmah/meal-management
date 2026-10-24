using System.Globalization;
using ClosedXML.Excel;
using MealManagement.Api.Data;
using MealManagement.Api.Dtos;
using MealManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Services;

/// <summary>
/// Imports employees from Excel. Rows are matched by Employee Code: new codes are registered, existing ones updated.
/// Departments and designations named in the file are created when missing (or reactivated when inactive).
/// Rows with errors are skipped; everything else is saved in one transaction.
/// </summary>
public class EmployeeImportService
{
    public const int MaxRows = 5000;
    public const long MaxFileBytes = 5 * 1024 * 1024;

    // Column header → field. Headers are matched ignoring case, spaces, '*' and punctuation.
    private static readonly Dictionary<string, string> HeaderMap = new()
    {
        ["employeecode"] = "code", ["code"] = "code", ["empcode"] = "code", ["employeeid"] = "code", ["empid"] = "code",
        ["employeename"] = "name", ["name"] = "name", ["empname"] = "name",
        ["department"] = "dept", ["dept"] = "dept",
        ["designation"] = "desig",
        ["mobileno"] = "mobile", ["mobile"] = "mobile", ["mobilenumber"] = "mobile", ["phone"] = "mobile", ["contactno"] = "mobile",
        ["email"] = "email", ["emailaddress"] = "email",
        ["status"] = "status",
        ["defaultmeal"] = "default", ["defaultmealeligible"] = "default", ["defaulteligible"] = "default", ["eligible"] = "default",
        ["eligiblefrom"] = "from", ["effectivedate"] = "from", ["mealfrom"] = "from",
    };

    private static readonly string[] Required = { "code", "name", "dept", "desig", "mobile" };
    private static readonly string[] DateFormats = { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "d-MMM-yyyy", "dd-MMM-yyyy", "d MMM yyyy" };

    private readonly MealDbContext _db;
    private readonly SettingsService _settings;
    private readonly MealGenerationService _generation;

    public EmployeeImportService(MealDbContext db, SettingsService settings, MealGenerationService generation)
    {
        _db = db;
        _settings = settings;
        _generation = generation;
    }

    private sealed class Row
    {
        public int RowNo;
        public string Code = "", Name = "", Dept = "", Desig = "", Mobile = "", Email = "", Status = "", Default = "", From = "";
        public DateOnly? EligibleFrom;
        public string Action = ImportAction.Error;
        public List<string> Messages = new();
        public Employee? Existing;
        public bool HasError => Messages.Any(m => m.StartsWith("Error:"));
        public void Error(string m) => Messages.Add("Error: " + m);
    }

    // ------------------------------------------------------------------
    public async Task<EmployeeImportResultDto> RunAsync(Stream file, bool commit, int adminId)
    {
        var rows = ReadRows(file);
        var settings = await _settings.GetAsync();
        var today = settings.Today;

        // Lookups (case-insensitive)
        var depts = await _db.Departments.ToListAsync();
        var desigs = await _db.Designations.ToListAsync();
        var codes = rows.Select(r => r.Code).Where(c => c.Length > 0).Distinct().ToList();
        var mobiles = rows.Select(r => r.Mobile).Where(m => m.Length > 0).Distinct().ToList();
        var existing = await _db.Employees
            .Where(x => codes.Contains(x.EmployeeCode) || mobiles.Contains(x.MobileNo))
            .ToListAsync();
        var byCode = existing.GroupBy(x => x.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byMobile = existing.GroupBy(x => x.MobileNo).ToDictionary(g => g.Key, g => g.First());
        var regular = await _db.MealTypes.Where(x => x.IsRegular && x.IsActive).OrderBy(x => x.SortOrder).FirstOrDefaultAsync();

        var codeCount = rows.Where(r => r.Code.Length > 0).GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var mobileCount = rows.Where(r => r.Mobile.Length > 0).GroupBy(r => r.Mobile).ToDictionary(g => g.Key, g => g.Count());

        foreach (var r in rows) Validate(r, byCode, byMobile, codeCount, mobileCount, regular, today, adminId);

        // Departments / designations needed by valid rows
        var valid = rows.Where(r => !r.HasError).ToList();
        var newDepts = Missing(valid.Select(r => r.Dept), depts.Select(d => d.Name));
        var newDesigs = Missing(valid.Select(r => r.Desig), desigs.Select(d => d.Name));
        var reactDepts = depts.Where(d => !d.IsActive && valid.Any(r => Same(r.Dept, d.Name))).Select(d => d.Name).ToList();
        var reactDesigs = desigs.Where(d => !d.IsActive && valid.Any(r => Same(r.Desig, d.Name))).Select(d => d.Name).ToList();

        // Work out New / Update / Unchanged
        foreach (var r in valid)
        {
            if (r.Existing == null) { r.Action = ImportAction.New; continue; }
            var e = r.Existing;
            var changes = new List<string>();
            var deptName = depts.FirstOrDefault(d => d.DepartmentId == e.DepartmentId)?.Name ?? "";
            var desigName = desigs.FirstOrDefault(d => d.DesignationId == e.DesignationId)?.Name ?? "";
            if (e.EmployeeName != r.Name) changes.Add($"Name: {e.EmployeeName} → {r.Name}");
            if (!Same(deptName, r.Dept)) changes.Add($"Department: {deptName} → {r.Dept}");
            if (!Same(desigName, r.Desig)) changes.Add($"Designation: {desigName} → {r.Desig}");
            if (e.MobileNo != r.Mobile) changes.Add($"Mobile: {e.MobileNo} → {r.Mobile}");
            if ((e.Email ?? "") != r.Email) changes.Add($"Email: {(e.Email ?? "—")} → {(r.Email.Length > 0 ? r.Email : "—")}");
            if (r.Status.Length > 0 && e.Status != r.Status) changes.Add($"Status: {e.Status} → {r.Status}");
            r.Action = changes.Count > 0 ? ImportAction.Update : ImportAction.Unchanged;
            r.Messages.InsertRange(0, changes);
        }

        var result = new EmployeeImportResultDto
        {
            TotalRows = rows.Count,
            NewCount = rows.Count(r => r.Action == ImportAction.New),
            UpdateCount = rows.Count(r => r.Action == ImportAction.Update),
            UnchangedCount = rows.Count(r => r.Action == ImportAction.Unchanged),
            ErrorCount = rows.Count(r => r.Action == ImportAction.Error),
            NewDepartments = newDepts,
            NewDesignations = newDesigs,
            ReactivatedDepartments = reactDepts,
            ReactivatedDesignations = reactDesigs,
            Rows = rows.Select(r => new EmployeeImportRowDto(r.RowNo, r.Code, r.Name, r.Dept, r.Desig, r.Mobile,
                r.Action, r.Messages)).ToList(),
        };
        if (!commit) return result;

        // ---------------- Save ----------------
        if (result.NewCount + result.UpdateCount == 0 && newDepts.Count + reactDepts.Count + newDesigs.Count + reactDesigs.Count == 0)
        {
            result.Committed = true;
            return result;
        }

        var deptByName = new Dictionary<string, Department>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in depts) deptByName.TryAdd(d.Name.Trim(), d);
        var desigByName = new Dictionary<string, Designation>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in desigs) desigByName.TryAdd(d.Name.Trim(), d);

        Department DeptFor(string name)
        {
            if (!deptByName.TryGetValue(name, out var d))
            {
                d = new Department { Name = name, IsActive = true };
                _db.Departments.Add(d);
                deptByName[name] = d;
            }
            d.IsActive = true;
            return d;
        }
        Designation DesigFor(string name)
        {
            if (!desigByName.TryGetValue(name, out var d))
            {
                d = new Designation { Name = name, IsActive = true };
                _db.Designations.Add(d);
                desigByName[name] = d;
            }
            d.IsActive = true;
            return d;
        }

        var now = DateTime.UtcNow;
        var created = new List<Employee>();
        foreach (var r in valid)
        {
            var dept = DeptFor(r.Dept);
            var desig = DesigFor(r.Desig);
            if (r.Action == ImportAction.Unchanged) continue;

            var e = r.Existing;
            if (e == null)
            {
                e = new Employee
                {
                    EmployeeCode = r.Code, Role = Roles.Employee, CreatedAt = now, CreatedBy = adminId,
                    Status = r.Status.Length > 0 ? r.Status : EmployeeStatus.Active,
                };
                _db.Employees.Add(e);
                created.Add(e);
                var wantsDefault = r.Default.Length == 0 || IsYes(r.Default);
                if (wantsDefault && regular != null)
                {
                    e.Eligibilities.Add(new MealEligibility
                    {
                        MealTypeId = regular.MealTypeId, EffectiveDate = r.EligibleFrom ?? today.AddDays(1),
                        Remarks = "Excel import", CreatedAt = now, CreatedBy = adminId,
                    });
                }
            }
            else
            {
                e.UpdatedAt = now;
                e.UpdatedBy = adminId;
                if (r.Status.Length > 0) e.Status = r.Status;
            }
            e.EmployeeName = r.Name;
            e.Department = dept;
            e.Designation = desig;
            e.MobileNo = r.Mobile;
            e.Email = r.Email.Length > 0 ? r.Email : null;
        }

        await _db.SaveChangesAsync(); // one transaction: departments, designations, employees, eligibilities
        await _generation.RefreshFutureAsync();

        result.Committed = true;
        result.CreatedEmployeeIds = created.Select(x => x.EmployeeId).ToList();
        return result;
    }

    // ------------------------------------------------------------------
    private void Validate(Row r, Dictionary<string, Employee> byCode, Dictionary<string, Employee> byMobile,
        Dictionary<string, int> codeCount, Dictionary<string, int> mobileCount, MealType? regular, DateOnly today, int adminId)
    {
        if (r.Code.Length == 0) r.Error("Employee Code is required.");
        else if (r.Code.Length > 30) r.Error("Employee Code is longer than 30 characters.");
        else if (codeCount[r.Code] > 1) r.Error($"Employee Code {r.Code} appears more than once in the file.");

        if (r.Name.Length == 0) r.Error("Employee Name is required.");
        else if (r.Name.Length > 150) r.Error("Employee Name is longer than 150 characters.");
        if (r.Dept.Length == 0) r.Error("Department is required.");
        else if (r.Dept.Length > 100) r.Error("Department is longer than 100 characters.");
        if (r.Desig.Length == 0) r.Error("Designation is required.");
        else if (r.Desig.Length > 100) r.Error("Designation is longer than 100 characters.");
        if (r.Email.Length > 150) r.Error("Email is longer than 150 characters.");
        else if (r.Email.Length > 0 && !r.Email.Contains('@')) r.Error("Email is not valid.");

        if (r.Mobile.Length == 0) r.Error("Mobile No is required.");
        else if (!MobileNumber.IsValid(r.Mobile)) r.Error($"Mobile No {r.Mobile} is not valid.");
        else if (mobileCount[r.Mobile] > 1) r.Error($"Mobile No {r.Mobile} appears more than once in the file.");

        if (r.Status.Length > 0)
        {
            if (r.Status.Equals("active", StringComparison.OrdinalIgnoreCase)) r.Status = EmployeeStatus.Active;
            else if (r.Status.Replace(" ", "").Equals("inactive", StringComparison.OrdinalIgnoreCase)) r.Status = EmployeeStatus.Inactive;
            else r.Error("Status must be Active or InActive.");
        }
        if (r.Default.Length > 0 && !IsYes(r.Default) && !IsNo(r.Default)) r.Error("Default Meal must be Yes or No.");
        if (r.From.Length > 0 && r.EligibleFrom == null) r.Error($"Eligible From '{r.From}' is not a valid date (use dd/mm/yyyy).");

        if (r.Code.Length > 0) byCode.TryGetValue(r.Code, out r.Existing);
        if (r.Existing != null && r.EligibleFrom.HasValue)
            r.Messages.Add("Note: Default Meal / Eligible From apply to new employees only. Use Eligibility to change it.");
        if (r.Existing == null && (r.Default.Length == 0 || IsYes(r.Default)) && regular == null)
            r.Error("No active regular meal type (e.g. Lunch) is configured for default eligibility.");

        if (r.Mobile.Length > 0 && byMobile.TryGetValue(r.Mobile, out var owner) && owner.EmployeeId != r.Existing?.EmployeeId)
            r.Error($"Mobile No {r.Mobile} belongs to another employee ({owner.EmployeeCode} – {owner.EmployeeName}).");

        if (r.Existing != null && r.Existing.EmployeeId == adminId && r.Status == EmployeeStatus.Inactive)
            r.Error("You cannot deactivate your own account.");

        if (r.HasError) r.Action = ImportAction.Error;
    }

    private List<Row> ReadRows(Stream file)
    {
        XLWorkbook wb;
        try { wb = new XLWorkbook(file); }
        catch { throw new AppException("Could not read the file. Please upload an Excel workbook (.xlsx)."); }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Employees", StringComparison.OrdinalIgnoreCase))
                     ?? wb.Worksheets.First();
            var used = ws.RangeUsed() ?? throw new AppException("The sheet is empty.");

            // Find the header row within the first 10 rows (the one with the most known headers).
            int headerRow = 0, best = 0;
            var firstRow = used.FirstRow().RowNumber();
            var lastCol = used.LastColumn().ColumnNumber();
            for (var rn = firstRow; rn < firstRow + 10 && rn <= used.LastRow().RowNumber(); rn++)
            {
                var hits = Enumerable.Range(1, lastCol).Count(c => HeaderMap.ContainsKey(Key(ws.Cell(rn, c).GetString())));
                if (hits > best) { best = hits; headerRow = rn; }
            }
            var columns = new Dictionary<string, int>();
            if (headerRow > 0)
                for (var c = 1; c <= lastCol; c++)
                    if (HeaderMap.TryGetValue(Key(ws.Cell(headerRow, c).GetString()), out var field)) columns.TryAdd(field, c);

            var missing = Required.Where(f => !columns.ContainsKey(f)).Select(Label).ToList();
            if (missing.Count > 0)
                throw new AppException($"Missing column(s): {string.Join(", ", missing)}. Please use the template.");

            var rows = new List<Row>();
            for (var rn = headerRow + 1; rn <= used.LastRow().RowNumber(); rn++)
            {
                string Get(string f) => columns.TryGetValue(f, out var c) ? Text(ws.Cell(rn, c)) : "";
                var r = new Row
                {
                    RowNo = rn,
                    Code = Get("code"), Name = Clean(Get("name")), Dept = Clean(Get("dept")), Desig = Clean(Get("desig")),
                    Mobile = NormalizeMobile(Get("mobile")), Email = Get("email"), Status = Get("status"), Default = Get("default"),
                };
                if (columns.TryGetValue("from", out var fc)) { r.EligibleFrom = ReadDate(ws.Cell(rn, fc)); r.From = Text(ws.Cell(rn, fc)); }
                if (new[] { r.Code, r.Name, r.Dept, r.Desig, r.Mobile, r.Email }.All(string.IsNullOrEmpty)) continue; // blank line
                rows.Add(r);
                if (rows.Count > MaxRows) throw new AppException($"The file has more than {MaxRows} employees. Please split it.");
            }
            if (rows.Count == 0) throw new AppException("No employee rows found under the header row.");
            return rows;
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Template workbook: Employees sheet (with an example row), Lists of current departments/designations, Instructions.</summary>
    public async Task<byte[]> BuildTemplateAsync()
    {
        var depts = await _db.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync();
        var desigs = await _db.Designations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Employees");
        string[] headers = { "Employee Code*", "Employee Name*", "Department*", "Designation*", "Mobile No*", "Email", "Status", "Default Meal", "Eligible From" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var head = ws.Range(1, 1, 1, headers.Length);
        head.Style.Font.Bold = true;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3EDD7");
        head.Style.Font.FontColor = XLColor.FromHtml("#3D5A28");
        ws.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDEEDB");

        // Code and mobile as text so leading zeros are kept.
        ws.Range("A2:A5001").Style.NumberFormat.Format = "@";
        ws.Range("E2:E5001").Style.NumberFormat.Format = "@";
        ws.Range("I2:I5001").Style.DateFormat.Format = "dd/mm/yyyy";
        ws.Range("G2:G5001").CreateDataValidation().List("\"Active,InActive\"", true);
        ws.Range("H2:H5001").CreateDataValidation().List("\"Yes,No\"", true);

        ws.Cell(2, 1).Value = "00001";
        ws.Cell(2, 2).Value = "Example Employee (delete this row)";
        ws.Cell(2, 3).Value = depts.FirstOrDefault() ?? "Human Resources";
        ws.Cell(2, 4).Value = desigs.FirstOrDefault() ?? "Executive";
        ws.Cell(2, 5).Value = "01700000001";
        ws.Cell(2, 7).Value = "Active";
        ws.Cell(2, 8).Value = "Yes";
        ws.Range(2, 1, 2, headers.Length).Style.Font.FontColor = XLColor.FromHtml("#8A8A8A");
        ws.Columns(1, headers.Length).Width = 18;
        ws.Column(2).Width = 32;
        ws.SheetView.FreezeRows(1);

        var lists = wb.AddWorksheet("Lists");
        lists.Cell(1, 1).Value = "Departments in the system";
        lists.Cell(1, 2).Value = "Designations in the system";
        lists.Row(1).Style.Font.Bold = true;
        for (var i = 0; i < depts.Count; i++) lists.Cell(i + 2, 1).Value = depts[i];
        for (var i = 0; i < desigs.Count; i++) lists.Cell(i + 2, 2).Value = desigs[i];
        lists.Columns(1, 2).Width = 34;

        var help = wb.AddWorksheet("Instructions");
        string[] lines =
        {
            "How to fill in the Employees sheet",
            "",
            "Columns marked * are required: Employee Code, Employee Name, Department, Designation, Mobile No.",
            "Employee Code: an existing code updates that employee; a new code registers a new employee.",
            "Department / Designation: type the name. Names not in the system are created automatically (see the Lists sheet for current names; matching ignores upper/lower case).",
            "Mobile No: 11 digits, e.g. 01711000000. It must be unique per employee.",
            "Status: Active or InActive. Blank = Active for new employees, unchanged for existing ones.",
            "Default Meal: Yes or No (new employees only). Yes = lunch is turned ON automatically. Blank = Yes.",
            "Eligible From: date default meal starts (new employees only), dd/mm/yyyy. Blank = tomorrow.",
            "Role (Admin) is never changed by the import; set it on the Employees page.",
            "",
            "After the import, new employees need an activation code to sign in. Download the codes from the import result.",
        };
        for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;
        help.Column(1).Width = 120;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ---------------- helpers ----------------
    private static string Key(string header) =>
        new string(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Label(string field) => field switch
    {
        "code" => "Employee Code", "name" => "Employee Name", "dept" => "Department",
        "desig" => "Designation", "mobile" => "Mobile No", _ => field,
    };

    private static string Text(IXLCell cell)
    {
        if (cell.IsEmpty()) return "";
        if (cell.DataType == XLDataType.Number)
        {
            var d = cell.GetDouble();
            return d == Math.Floor(d) ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture);
        }
        return cell.GetFormattedString().Trim();
    }

    private static string Clean(string s) => string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Excel often drops the leading 0 of mobile numbers stored as numbers (1711000000 → 01711000000).</summary>
    private static string NormalizeMobile(string raw)
    {
        var m = MobileNumber.Normalize(raw);
        if (m.Length == 10 && m.StartsWith('1')) m = "0" + m;
        return m;
    }

    private static DateOnly? ReadDate(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
        if (cell.DataType == XLDataType.Number)
        {
            try { return DateOnly.FromDateTime(DateTime.FromOADate(cell.GetDouble())); } catch { return null; }
        }
        var s = cell.GetFormattedString().Trim();
        return DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? DateOnly.FromDateTime(dt) : null;
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool IsYes(string s) => s.Trim().ToLowerInvariant() is "yes" or "y" or "true" or "1";
    private static bool IsNo(string s) => s.Trim().ToLowerInvariant() is "no" or "n" or "false" or "0";

    private static List<string> Missing(IEnumerable<string> wanted, IEnumerable<string> existing)
    {
        var have = new HashSet<string>(existing.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        return wanted.Where(w => w.Length > 0 && !have.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }
}
