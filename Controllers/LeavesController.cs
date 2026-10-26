using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

public class LeavesController : BaseController
{
    public static readonly string[] Types = { "اعتيادية", "مرضية", "زمنية", "بدون راتب", "أخرى" };
    public LeavesController(AppDb db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        var q = Db.Leaves.Include(l => l.Employee).ThenInclude(e => e.Branch).ThenInclude(b => b.Region).AsQueryable();
        if (!IsAdmin) { var r = MyRegionId; q = q.Where(l => l.Employee.Branch.RegionId == r); }
        return View(await q.OrderByDescending(l => l.FromDate).Take(300).ToListAsync());
    }

    async Task FillLists()
    {
        var emps = await ScopedEmployees().OrderBy(e => e.Branch.Region.Name).ThenBy(e => e.Branch.Name).ThenByDescending(e => e.IsPrimary).ToListAsync();
        ViewBag.Employees = emps.Select(e => new SelectListItem($"{e.Branch.Name} — {e.FullName} ({(e.IsPrimary ? "أساسي" : "بديل")})", e.Id.ToString())).ToList();
        ViewBag.Types = Types;
    }

    [HttpGet] public async Task<IActionResult> Create() { await FillLists(); return View(); }

    [HttpPost]
    public async Task<IActionResult> Create(int employeeId, DateOnly fromDate, DateOnly toDate, string type, string notes)
    {
        if (!await ScopedEmployees().AnyAsync(e => e.Id == employeeId)) return Forbid();
        if (toDate < fromDate) { ViewBag.Error = "تاريخ النهاية يجب ألا يسبق تاريخ البداية"; await FillLists(); return View(); }
        Db.Leaves.Add(new LeaveRecord { EmployeeId = employeeId, FromDate = fromDate, ToDate = toDate, Type = type, Notes = notes?.Trim(), RecordedBy = User.Identity.Name });
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم تسجيل الإجازة";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await Db.Leaves.Include(x => x.Employee).ThenInclude(e => e.Branch).FirstOrDefaultAsync(x => x.Id == id);
        if (l == null) return NotFound();
        if (!IsAdmin && l.Employee.Branch.RegionId != MyRegionId) return Forbid();
        Db.Leaves.Remove(l);
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم حذف الإجازة";
        return RedirectToAction(nameof(Index));
    }
}
