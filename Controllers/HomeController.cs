using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

public class HomeController : BaseController
{
    public HomeController(AppDb db) : base(db) { }

    public async Task<IActionResult> Index(DateOnly? date, int? regionId)
    {
        var d = date ?? Ui.Today;
        var bq = ScopedBranches();
        if (IsAdmin && regionId != null) bq = bq.Where(b => b.RegionId == regionId);
        var branches = await bq.Include(b => b.Region).Include(b => b.Employees.Where(e => e.IsActive)).ToListAsync();
        var map = await Seeder.Map(Db, branches.SelectMany(b => b.Employees).Select(e => e.Id), d);

        var rows = branches.Select(b =>
        {
            var p = b.Employees.FirstOrDefault(e => e.IsPrimary);
            var s = b.Employees.FirstOrDefault(e => !e.IsPrimary);
            var ps = p == null ? DayStatus.Unrecorded : map[p.Id].Status;
            var bs = s == null ? DayStatus.Unrecorded : map[s.Id].Status;
            return new BranchRow { Branch = b, Primary = p, Backup = s, PStatus = ps, BStatus = bs, Coverage = Ui.Coverage(p, ps, s, bs) };
        }).OrderBy(r => Ui.CoverageOrder(r.Coverage)).ThenBy(r => r.Branch.Region.Name).ThenBy(r => r.Branch.Name).ToList();

        return View(new DashVm { Date = d, RegionId = regionId, Regions = IsAdmin ? await Db.Regions.OrderBy(r => r.Name).ToListAsync() : new(), Rows = rows });
    }
}
