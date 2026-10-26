using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

public class AttendanceController : BaseController
{
    public AttendanceController(AppDb db) : base(db) { }

    public async Task<IActionResult> Day(DateOnly? date, int? regionId)
    {
        var d = date ?? Ui.Today;
        var q = ScopedEmployees();
        if (IsAdmin && regionId != null) q = q.Where(e => e.Branch.RegionId == regionId);
        var emps = await q.OrderBy(e => e.Branch.Region.Name).ThenBy(e => e.Branch.Name).ThenByDescending(e => e.IsPrimary).ToListAsync();
        var map = await Seeder.Map(Db, emps.Select(e => e.Id), d);
        var items = emps.Select(e => new DayItem { EmployeeId = e.Id, Name = e.FullName, Branch = e.Branch.Name, Region = e.Branch.Region.Name, IsPrimary = e.IsPrimary, Status = map[e.Id].Status, Notes = map[e.Id].Note }).ToList();
        return View(new DayVm { Date = d, RegionId = regionId, Regions = IsAdmin ? await Db.Regions.OrderBy(r => r.Name).ToListAsync() : new(), Items = items });
    }

    [HttpPost]
    public async Task<IActionResult> Save(SaveVm m)
    {
        var allowed = (await ScopedEmployees().Select(e => e.Id).ToListAsync()).ToHashSet();
        var rows = (m.Rows ?? new()).Where(r => allowed.Contains(r.EmployeeId)).ToList();
        var ids = rows.Select(r => r.EmployeeId).ToList();
        var existing = await Db.Attendance.Where(a => a.Date == m.Date && ids.Contains(a.EmployeeId)).ToDictionaryAsync(a => a.EmployeeId);
        foreach (var r in rows)
        {
            existing.TryGetValue(r.EmployeeId, out var a);
            if (r.Status == DayStatus.Unrecorded) { if (a != null) Db.Attendance.Remove(a); continue; }
            if (a == null) { a = new AttendanceRecord { EmployeeId = r.EmployeeId, Date = m.Date }; Db.Attendance.Add(a); }
            a.Status = r.Status; a.Notes = r.Notes?.Trim(); a.RecordedBy = User.Identity.Name;
        }
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم حفظ الدوام";
        return RedirectToAction(nameof(Day), new { date = m.Date.ToString("yyyy-MM-dd"), regionId = m.RegionId });
    }

    public async Task<IActionResult> Employee(int id)
    {
        var e = await ScopedEmployees().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        var from = Ui.Today.AddDays(-60);
        var recs = await Db.Attendance.Where(a => a.EmployeeId == id && a.Date >= from).OrderByDescending(a => a.Date).ToListAsync();
        var leaves = await Db.Leaves.Where(l => l.EmployeeId == id).OrderByDescending(l => l.FromDate).Take(20).ToListAsync();
        return View(new EmpVm { Employee = e, Records = recs, Leaves = leaves });
    }
}
