using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MealManagement.Api.Data;
using MealManagement.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MealManagement.Api.Services;

public record ReportColumn(string Key, string Header, string Type = "text"); // text | number | money | date
public record ReportDefinition(string Code, string Name, string Description, bool NeedsDateRange, bool NeedsSingleDate);

public class ReportTable
{
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public List<ReportColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public Dictionary<string, object?> Totals { get; set; } = new();
}

public class ReportQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? Date { get; set; }
    public int? MealTypeId { get; set; }
    public int? DepartmentId { get; set; }
    public int? EmployeeId { get; set; }
}

public class ReportService
{
    private readonly MealDbContext _db;
    private readonly MealCalendarFactory _calendars;
    private readonly MealService _meals;

    public ReportService(MealDbContext db, MealCalendarFactory calendars, MealService meals)
    {
        _db = db;
        _calendars = calendars;
        _meals = meals;
    }

    public static readonly List<ReportDefinition> Definitions = new()
    {
        new("daily-summary", "Daily meal & cost summary", "Meals served per day with total, employee and company cost.", true, false),
        new("employee-statement", "Employee meal statement", "Meals and contribution per employee for the period (for payroll deduction).", true, false),
        new("department-summary", "Department summary", "Meals and cost split by department.", true, false),
        new("meal-list", "Meal list for a date", "Who has the meal ON for a date (kitchen / serving list).", false, true),
        new("employee-detail", "Employee meal detail", "Day-by-day meals and cost for one employee.", true, false),
    };

    public async Task<ReportTable> BuildAsync(string code, ReportQuery q)
    {
        return code switch
        {
            "daily-summary" => await DailySummaryAsync(q),
            "employee-statement" => await EmployeeStatementAsync(q),
            "department-summary" => await DepartmentSummaryAsync(q),
            "meal-list" => await MealListAsync(q),
            "employee-detail" => await EmployeeDetailAsync(q),
            _ => throw AppException.NotFound("Report")
        };
    }

    // ---------- data ----------
    private record OnRow(int EmployeeId, string Code, string Name, int DepartmentId, string Department, string Designation,
        DateOnly Date, int MealTypeId, string MealType, decimal Total, decimal Employee, decimal Company, string Source);

    private async Task<(List<OnRow> Rows, DateOnly From, DateOnly To)> LoadAsync(ReportQuery q)
    {
        var (from, to) = Range(q);
        var cal = await _calendars.CreateAsync(from, to, includeInactiveTypes: true);

        var query = _db.MealEntries.AsNoTracking().Where(x => x.IsOn && x.MealDate >= from && x.MealDate <= to);
        if (q.MealTypeId.HasValue) query = query.Where(x => x.MealTypeId == q.MealTypeId);
        if (q.DepartmentId.HasValue) query = query.Where(x => x.Employee!.DepartmentId == q.DepartmentId);
        if (q.EmployeeId.HasValue) query = query.Where(x => x.EmployeeId == q.EmployeeId);

        var raw = await query.Select(x => new
        {
            x.EmployeeId, x.Employee!.EmployeeCode, x.Employee.EmployeeName, x.Employee.DepartmentId,
            Department = x.Employee.Department!.Name, Designation = x.Employee.Designation!.Name,
            x.MealDate, x.MealTypeId, MealType = x.MealType!.Name, x.Source
        }).ToListAsync();

        var rows = raw.Select(r =>
        {
            var c = cal.Cost(r.MealDate, r.MealTypeId);
            return new OnRow(r.EmployeeId, r.EmployeeCode, r.EmployeeName, r.DepartmentId, r.Department, r.Designation,
                r.MealDate, r.MealTypeId, r.MealType, c.Total, c.Employee, c.Company, r.Source);
        }).ToList();
        return (rows, from, to);
    }

    private static (DateOnly From, DateOnly To) Range(ReportQuery q)
    {
        var from = q.From ?? q.Date ?? throw new AppException("Please choose a date range.");
        var to = q.To ?? q.Date ?? from;
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) throw new AppException("Please choose a period of one year or less.");
        return (from, to);
    }

    private static string Period(DateOnly from, DateOnly to) =>
        from == to ? from.ToString("dd MMM yyyy") : $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";

    // ---------- reports ----------
    private async Task<ReportTable> DailySummaryAsync(ReportQuery q)
    {
        var (rows, from, to) = await LoadAsync(q);
        var t = new ReportTable
        {
            Code = "daily-summary", Title = "Daily meal & cost summary", Subtitle = Period(from, to),
            Columns = new()
            {
                new("date", "Date", "date"), new("day", "Day"), new("mealType", "Meal"),
                new("meals", "Meals", "number"), new("costPerMeal", "Cost / meal", "money"),
                new("employeePerMeal", "Employee / meal", "money"), new("totalCost", "Total cost", "money"),
                new("employeeTotal", "Employee contribution", "money"), new("companyTotal", "Company contribution", "money"),
            }
        };
        foreach (var g in rows.GroupBy(r => (r.Date, r.MealTypeId, r.MealType)).OrderBy(g => g.Key.Date).ThenBy(g => g.Key.MealTypeId))
        {
            var first = g.First();
            t.Rows.Add(new()
            {
                ["date"] = g.Key.Date, ["day"] = g.Key.Date.DayOfWeek.ToString(), ["mealType"] = g.Key.MealType,
                ["meals"] = g.Count(), ["costPerMeal"] = first.Total, ["employeePerMeal"] = first.Employee,
                ["totalCost"] = g.Sum(x => x.Total), ["employeeTotal"] = g.Sum(x => x.Employee), ["companyTotal"] = g.Sum(x => x.Company),
            });
        }
        t.Totals = new()
        {
            ["date"] = "Total", ["meals"] = rows.Count, ["totalCost"] = rows.Sum(x => x.Total),
            ["employeeTotal"] = rows.Sum(x => x.Employee), ["companyTotal"] = rows.Sum(x => x.Company)
        };
        return t;
    }

    private async Task<ReportTable> EmployeeStatementAsync(ReportQuery q)
    {
        var (rows, from, to) = await LoadAsync(q);
        var t = new ReportTable
        {
            Code = "employee-statement", Title = "Employee meal statement", Subtitle = Period(from, to),
            Columns = new()
            {
                new("code", "Employee code"), new("name", "Employee name"), new("department", "Department"),
                new("designation", "Designation"), new("meals", "Meals", "number"),
                new("employeeTotal", "Employee contribution", "money"), new("companyTotal", "Company contribution", "money"),
                new("totalCost", "Total cost", "money"),
            }
        };
        foreach (var g in rows.GroupBy(r => r.EmployeeId).Select(g => g.ToList()).OrderBy(g => g[0].Code))
        {
            var e = g[0];
            t.Rows.Add(new()
            {
                ["code"] = e.Code, ["name"] = e.Name, ["department"] = e.Department, ["designation"] = e.Designation,
                ["meals"] = g.Count, ["employeeTotal"] = g.Sum(x => x.Employee), ["companyTotal"] = g.Sum(x => x.Company),
                ["totalCost"] = g.Sum(x => x.Total),
            });
        }
        t.Totals = new()
        {
            ["code"] = "Total", ["meals"] = rows.Count, ["employeeTotal"] = rows.Sum(x => x.Employee),
            ["companyTotal"] = rows.Sum(x => x.Company), ["totalCost"] = rows.Sum(x => x.Total)
        };
        return t;
    }

    private async Task<ReportTable> DepartmentSummaryAsync(ReportQuery q)
    {
        var (rows, from, to) = await LoadAsync(q);
        var t = new ReportTable
        {
            Code = "department-summary", Title = "Department summary", Subtitle = Period(from, to),
            Columns = new()
            {
                new("department", "Department"), new("employees", "Employees", "number"), new("meals", "Meals", "number"),
                new("employeeTotal", "Employee contribution", "money"), new("companyTotal", "Company contribution", "money"),
                new("totalCost", "Total cost", "money"),
            }
        };
        foreach (var g in rows.GroupBy(r => r.Department).OrderBy(g => g.Key))
        {
            t.Rows.Add(new()
            {
                ["department"] = g.Key, ["employees"] = g.Select(x => x.EmployeeId).Distinct().Count(), ["meals"] = g.Count(),
                ["employeeTotal"] = g.Sum(x => x.Employee), ["companyTotal"] = g.Sum(x => x.Company), ["totalCost"] = g.Sum(x => x.Total),
            });
        }
        t.Totals = new()
        {
            ["department"] = "Total", ["employees"] = rows.Select(x => x.EmployeeId).Distinct().Count(), ["meals"] = rows.Count,
            ["employeeTotal"] = rows.Sum(x => x.Employee), ["companyTotal"] = rows.Sum(x => x.Company), ["totalCost"] = rows.Sum(x => x.Total)
        };
        return t;
    }

    private async Task<ReportTable> MealListAsync(ReportQuery q)
    {
        var date = q.Date ?? q.From ?? throw new AppException("Please choose a date.");
        var mealTypeId = q.MealTypeId ?? await _db.MealTypes.Where(x => x.IsRegular && x.IsActive)
            .OrderBy(x => x.SortOrder).Select(x => x.MealTypeId).FirstOrDefaultAsync();
        // Uses the resolved board so future dates (not yet generated) are also correct.
        var board = await _meals.GetBoardAsync(date, mealTypeId, q.DepartmentId, null);
        var t = new ReportTable
        {
            Code = "meal-list", Title = $"{board.MealTypeName} list", Subtitle = date.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture),
            Columns = new()
            {
                new("sl", "SL", "number"), new("code", "Employee code"), new("name", "Employee name"),
                new("department", "Department"), new("designation", "Designation"), new("mobile", "Mobile no"), new("source", "Marked by"),
            }
        };
        var sl = 0;
        foreach (var r in board.Rows.Where(r => r.IsOn))
        {
            t.Rows.Add(new()
            {
                ["sl"] = ++sl, ["code"] = r.EmployeeCode, ["name"] = r.EmployeeName, ["department"] = r.Department,
                ["designation"] = r.Designation, ["mobile"] = r.MobileNo, ["source"] = r.Source == EntrySource.Auto ? "Default" : r.Source,
            });
        }
        t.Totals = new() { ["code"] = "Total meals", ["name"] = sl };
        return t;
    }

    private async Task<ReportTable> EmployeeDetailAsync(ReportQuery q)
    {
        if (!q.EmployeeId.HasValue) throw new AppException("Please choose an employee.");
        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == q.EmployeeId)
                  ?? throw AppException.NotFound("Employee");
        var (rows, from, to) = await LoadAsync(q);
        var t = new ReportTable
        {
            Code = "employee-detail", Title = $"{emp.EmployeeName} ({emp.EmployeeCode})", Subtitle = Period(from, to),
            Columns = new()
            {
                new("date", "Date", "date"), new("day", "Day"), new("mealType", "Meal"), new("source", "Marked by"),
                new("totalCost", "Meal cost", "money"), new("employeeTotal", "Employee contribution", "money"),
                new("companyTotal", "Company contribution", "money"),
            }
        };
        foreach (var r in rows.OrderBy(r => r.Date).ThenBy(r => r.MealTypeId))
        {
            t.Rows.Add(new()
            {
                ["date"] = r.Date, ["day"] = r.Date.DayOfWeek.ToString(), ["mealType"] = r.MealType,
                ["source"] = r.Source == EntrySource.Auto ? "Default" : r.Source,
                ["totalCost"] = r.Total, ["employeeTotal"] = r.Employee, ["companyTotal"] = r.Company,
            });
        }
        t.Totals = new()
        {
            ["date"] = $"{rows.Count} meals", ["totalCost"] = rows.Sum(x => x.Total),
            ["employeeTotal"] = rows.Sum(x => x.Employee), ["companyTotal"] = rows.Sum(x => x.Company)
        };
        return t;
    }

    // ---------- export ----------
    public static byte[] ToExcel(ReportTable t, string companyName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Report");
        var n = t.Columns.Count;

        ws.Cell(1, 1).Value = companyName;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14).Font.SetFontColor(XLColor.FromHtml("#2b6777"));
        ws.Cell(2, 1).Value = t.Title;
        ws.Cell(2, 1).Style.Font.SetBold().Font.SetFontSize(12);
        ws.Cell(3, 1).Value = t.Subtitle;
        ws.Cell(3, 1).Style.Font.SetFontColor(XLColor.FromHtml("#5b7480"));

        var headerRow = 5;
        for (var i = 0; i < n; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = t.Columns[i].Header;
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2b6777"))
                .Alignment.SetWrapText(true);
        }

        var r = headerRow + 1;
        foreach (var row in t.Rows)
        {
            for (var i = 0; i < n; i++) SetCell(ws.Cell(r, i + 1), t.Columns[i], row.GetValueOrDefault(t.Columns[i].Key));
            r++;
        }

        if (t.Totals.Count > 0)
        {
            for (var i = 0; i < n; i++) SetCell(ws.Cell(r, i + 1), t.Columns[i], t.Totals.GetValueOrDefault(t.Columns[i].Key));
            ws.Range(r, 1, r, n).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#e6eff4"))
                .Border.SetTopBorder(XLBorderStyleValues.Thin);
        }

        ws.Range(headerRow, 1, Math.Max(headerRow, r), n).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetOutsideBorderColor(XLColor.FromHtml("#c8d8e4"));
        ws.SheetView.FreezeRows(headerRow);
        ws.Columns(1, n).AdjustToContents(headerRow, r, 10, 45);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetCell(IXLCell cell, ReportColumn col, object? value)
    {
        switch (value)
        {
            case null: return;
            case DateOnly d:
                cell.Value = d.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd-MMM-yyyy";
                return;
            case decimal m:
                cell.Value = m;
                if (col.Type == "money") cell.Style.NumberFormat.Format = "#,##0.00";
                return;
            case int i:
                cell.Value = i;
                return;
            default:
                cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                return;
        }
    }

    public static byte[] ToCsv(ReportTable t)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", t.Columns.Select(c => Esc(c.Header))));
        foreach (var row in t.Rows.Append(t.Totals).Where(x => x.Count > 0))
            sb.AppendLine(string.Join(",", t.Columns.Select(c => Esc(Format(row.GetValueOrDefault(c.Key))))));
        // UTF-8 BOM so Excel opens Bangla names correctly
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Format(object? v) => v switch
    {
        null => "",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
    };

    private static string Esc(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
