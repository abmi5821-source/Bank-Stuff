using System.Security.Claims;
using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : BaseController
{
    public AdminController(AppDb db) : base(db) { }

    public async Task<IActionResult> Users() => View(new UsersVm
    {
        Users = await Db.AppUsers.Include(u => u.Region).OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync(),
        Regions = await Db.Regions.OrderBy(r => r.Name).ToListAsync()
    });

    [HttpPost]
    public async Task<IActionResult> CreateUser(string username, string fullName, string password, string role, int? regionId)
    {
        username = (username ?? "").Trim().ToLowerInvariant();
        string err = null;
        if (username.Length < 3) err = "اسم المستخدم قصير جداً";
        else if ((password ?? "").Length < 8) err = "كلمة السر يجب ألا تقل عن 8 أحرف";
        else if (role != "Admin" && role != "Delegate") err = "الصلاحية غير صحيحة";
        else if (role == "Delegate" && regionId == null) err = "اختر المنطقة للمندوب";
        else if (await Db.AppUsers.AnyAsync(u => u.Username == username)) err = "اسم المستخدم موجود مسبقاً";
        if (err != null) { TempData["err"] = err; return RedirectToAction(nameof(Users)); }
        var u = new AppUser { Username = username, FullName = fullName?.Trim(), Role = role, RegionId = role == "Delegate" ? regionId : null };
        u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, password);
        Db.AppUsers.Add(u);
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم إنشاء المستخدم";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(int id, string password)
    {
        var u = await Db.AppUsers.FindAsync(id);
        if (u == null) return NotFound();
        if ((password ?? "").Length < 8) { TempData["err"] = "كلمة السر يجب ألا تقل عن 8 أحرف"; return RedirectToAction(nameof(Users)); }
        u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, password);
        await Db.SaveChangesAsync();
        TempData["msg"] = "تم تغيير كلمة السر";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (id.ToString() == User.FindFirst(ClaimTypes.NameIdentifier)?.Value) { TempData["err"] = "لا يمكنك حذف حسابك"; return RedirectToAction(nameof(Users)); }
        var u = await Db.AppUsers.FindAsync(id);
        if (u != null) { Db.AppUsers.Remove(u); await Db.SaveChangesAsync(); }
        TempData["msg"] = "تم حذف المستخدم";
        return RedirectToAction(nameof(Users));
    }

    public IActionResult Branches() => RedirectToAction("Index", "Branches");

    [HttpGet] public async Task<IActionResult> Import() { ViewBag.Regions = await Db.Regions.OrderBy(r => r.Name).ToListAsync(); return View(); }

    [HttpPost]
    public async Task<IActionResult> Import(string data)
    {
        var regions = await Db.Regions.ToListAsync();
        var log = new List<string>(); int ok = 0, n = 0;
        foreach (var raw in (data ?? "").Split('\n'))
        {
            n++;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var c = raw.Split(raw.Contains('\t') ? '\t' : ',').Select(x => x.Trim()).ToArray();
            if (c[0] == "المنطقة") continue; // سطر العناوين
            if (c.Length < 2) { log.Add($"سطر {n}: عدد الأعمدة غير كافٍ"); continue; }
            var region = regions.FirstOrDefault(r => r.Name == c[0]);
            if (region == null) { log.Add($"سطر {n}: المنطقة '{c[0]}' غير معروفة"); continue; }
            var br = await Db.Branches.Include(x => x.Employees).FirstOrDefaultAsync(x => x.RegionId == region.Id && x.Name == c[1]);
            if (br == null) { br = new Branch { Name = c[1], RegionId = region.Id }; Db.Branches.Add(br); }
            Upsert(br, true, c.ElementAtOrDefault(2), c.ElementAtOrDefault(3));
            Upsert(br, false, c.ElementAtOrDefault(4), c.ElementAtOrDefault(5));
            await Db.SaveChangesAsync();
            ok++;
        }
        ViewBag.Regions = await Db.Regions.OrderBy(r => r.Name).ToListAsync();
        ViewBag.Ok = ok; ViewBag.Log = log;
        return View();
    }

    static void Upsert(Branch br, bool primary, string name, string phone)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var e = br.Employees.FirstOrDefault(x => x.IsPrimary == primary && x.IsActive);
        if (e == null) { e = new Employee { IsPrimary = primary }; br.Employees.Add(e); }
        e.FullName = name; e.Phone = phone;
    }
}
