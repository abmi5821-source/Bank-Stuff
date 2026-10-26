using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

[Authorize(Roles = "Admin")]
public class ReportsController : BaseController
{
    public ReportsController(AppDb db) : base(db) { }

    public async Task<IActionResult> Index(int? regionId)
    {
        var q = Db.Employees.Include(e => e.Branch).ThenInclude(b => b.Region).Where(e => e.IsActive);
        if (regionId != null) q = q.Where(e => e.Branch.RegionId == regionId);
        ViewBag.Regions = await Db.Regions.OrderBy(r => r.Name).ToListAsync();
        ViewBag.RegionId = regionId;
        return View(await q.OrderBy(e => e.Branch.Region.Name).ThenBy(e => e.Branch.Name).ThenByDescending(e => e.IsPrimary).ToListAsync());
    }

    public async Task<IActionResult> Employee(int id, string period, DateOnly? date)
    {
        var emp = await Db.Employees.Include(x => x.Branch).ThenInclude(b => b.Region).FirstOrDefaultAsync(x => x.Id == id);
        if (emp == null) return NotFound();
        period = period is "week" or "season" or "year" ? period : "month";
        var a = date ?? Ui.Today;
        DateOnly from, to; string title;
        switch (period)
        {
            case "week": from = a.AddDays(-(int)a.DayOfWeek); to = from.AddDays(6); title = "أسبوع"; break;
            case "season":
                int sm = a.Month == 12 ? 12 : a.Month <= 2 ? 12 : a.Month <= 5 ? 3 : a.Month <= 8 ? 6 : 9;
                int sy = a.Month <= 2 ? a.Year - 1 : a.Year;
                from = new DateOnly(sy, sm, 1); to = from.AddMonths(3).AddDays(-1);
                title = "فصل " + sm switch { 12 => "الشتاء", 3 => "الربيع", 6 => "الصيف", _ => "الخريف" } + " " + sy; break;
            case "year": from = new DateOnly(a.Year, 1, 1); to = new DateOnly(a.Year, 12, 31); title = "سنة " + a.Year; break;
            default: from = new DateOnly(a.Year, a.Month, 1); to = from.AddMonths(1).AddDays(-1); title = "شهر " + a.ToString("yyyy-MM"); break;
        }
        var end = to > Ui.Today ? Ui.Today : to; // لا نحسب أياماً لم تأتِ بعد
        var days = new List<DateOnly>();
        for (var d = from; d <= end; d = d.AddDays(1)) days.Add(d);

        var att = await Db.Attendance.Where(x => x.EmployeeId == id && x.Date >= from && x.Date <= end).ToDictionaryAsync(x => x.Date, x => x.Status);
        var leaves = await Db.Leaves.Where(l => l.EmployeeId == id && l.FromDate <= end && l.ToDate >= from).ToListAsync();
        DayStatus St(DateOnly d) => att.TryGetValue(d, out var s) ? s : leaves.Any(l => l.FromDate <= d && l.ToDate >= d) ? DayStatus.Leave : DayStatus.Unrecorded;

        Func<DateOnly, (int, string)> bucket = period switch
        {
            "year" => d => (d.Month, d.ToString("yyyy-MM")),
            "season" => d => { var w = (d.DayNumber - from.DayNumber) / 7; return (w, from.AddDays(w * 7).ToString("MM-dd")); },
            _ => d => (d.DayNumber, d.ToString("MM-dd"))
        };
        var order = new[] { DayStatus.Present, DayStatus.Absent, DayStatus.Leave, DayStatus.Mission, DayStatus.Unrecorded };
        var vm = new ReportVm { Employee = emp, Period = period, Title = title, Date = a, From = from, To = to, Series = order.Select(_ => Array.Empty<int>()).ToList() };
        var groups = days.GroupBy(d => bucket(d).Item1).OrderBy(g => g.Key).ToList();
        vm.Labels = groups.Select(g => bucket(g.First()).Item2).ToList();
        vm.Series = order.Select(s => groups.Select(g => g.Count(d => St(d) == s)).ToArray()).ToList();
        vm.Totals = vm.Series.Select(x => x.Sum()).ToArray();
        var worked = vm.Totals[0] + vm.Totals[3]; var recorded = worked + vm.Totals[1] + vm.Totals[2];
        vm.Rate = recorded == 0 ? 0 : (int)Math.Round(100.0 * worked / recorded);
        vm.Prev = period switch { "week" => a.AddDays(-7), "year" => a.AddYears(-1), "season" => from.AddDays(-1), _ => a.AddMonths(-1) };
        vm.Next = period switch { "week" => a.AddDays(7), "year" => a.AddYears(1), "season" => to.AddDays(1), _ => a.AddMonths(1) };
        return View(vm);
    }
}
