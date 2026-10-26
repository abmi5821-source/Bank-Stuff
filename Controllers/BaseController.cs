using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

[Authorize]
public abstract class BaseController : Controller
{
    protected readonly AppDb Db;
    protected BaseController(AppDb db) { Db = db; }
    protected bool IsAdmin => User.IsInRole("Admin");
    protected int? MyRegionId => int.TryParse(User.FindFirst("region")?.Value, out var r) ? r : null;

    // المندوب يرى فقط فروع منطقته؛ مسؤول القسم يرى كل شيء
    protected IQueryable<Branch> ScopedBranches()
    {
        var q = Db.Branches.AsQueryable();
        if (!IsAdmin) { var r = MyRegionId; q = q.Where(b => b.RegionId == r); }
        return q;
    }
    protected IQueryable<Employee> ScopedEmployees()
    {
        var q = Db.Employees.Include(e => e.Branch).ThenInclude(b => b.Region).Where(e => e.IsActive);
        if (!IsAdmin) { var r = MyRegionId; q = q.Where(e => e.Branch.RegionId == r); }
        return q;
    }
}
