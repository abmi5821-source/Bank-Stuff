using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

public class BranchesController : BaseController
{
    public BranchesController(AppDb db) : base(db) { }

    public async Task<IActionResult> Index()
    {
        ViewBag.Regions = await Db.Regions.OrderBy(r => r.Name).ToListAsync();
        return View(await ScopedBranches().Include(b => b.Region).Include(b => b.Employees.Where(e => e.IsActive)).OrderBy(b => b.Region.Name).ThenBy(b => b.Name).ToListAsync());
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddBranch(string name, int regionId)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || !await Db.Regions.AnyAsync(r => r.Id == regionId)) TempData["err"] = "بيانات الفرع غير صحيحة";
        else if (await Db.Branches.AnyAsync(b => b.RegionId == regionId && b.Name == name)) TempData["err"] = "الفرع موجود مسبقاً";
        else { Db.Branches.Add(new Branch { Name = name, RegionId = regionId }); await Db.SaveChangesAsync(); TempData["msg"] = "تمت إضافة الفرع"; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBranch(int id)
    {
        var b = await Db.Branches.FindAsync(id);
        if (b != null) { Db.Branches.Remove(b); await Db.SaveChangesAsync(); TempData["msg"] = "تم حذف الفرع وسجلاته"; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AddEmployee(int branchId, bool isPrimary, string fullName, string phone)
    {
        var b = await ScopedBranches().Include(x => x.Employees).FirstOrDefaultAsync(x => x.Id == branchId);
        if (b == null) return Forbid();
        if (string.IsNullOrWhiteSpace(fullName)) { TempData["err"] = "اكتب اسم الموظف"; return RedirectToAction(nameof(Index)); }
        if (b.Employees.Any(e => e.IsActive && e.IsPrimary == isPrimary)) { TempData["err"] = "يوجد موظف في هذه الخانة، احذفه أولاً"; return RedirectToAction(nameof(Index)); }
        b.Employees.Add(new Employee { FullName = fullName.Trim(), Phone = phone?.Trim(), IsPrimary = isPrimary });
        await Db.SaveChangesAsync();
        TempData["msg"] = "تمت إضافة الموظف";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await ScopedEmployees().FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? NotFound() : View(e);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, string fullName, string phone)
    {
        var e = await ScopedEmployees().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        if (string.IsNullOrWhiteSpace(fullName)) { TempData["err"] = "اكتب اسم الموظف"; return RedirectToAction(nameof(Edit), new { id }); }
        e.FullName = fullName.Trim(); e.Phone = phone?.Trim();
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم حفظ التعديل";
        return RedirectToAction(nameof(Index));
    }

    // الحذف إخفاء من الفرع مع بقاء السجل التاريخي للدوام والإجازات
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await ScopedEmployees().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        e.IsActive = false;
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم حذف الموظف من الفرع";
        return RedirectToAction(nameof(Index));
    }
}
