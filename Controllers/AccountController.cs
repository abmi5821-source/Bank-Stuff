using System.Security.Claims;
using BankLiaison.Data;
using BankLiaison.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Controllers;

public class AccountController : Controller
{
    private readonly AppDb _db;
    public AccountController(AppDb db) { _db = db; }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string returnUrl) { ViewBag.ReturnUrl = returnUrl; return View(); }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(string username, string password, string returnUrl)
    {
        var name = (username ?? "").Trim().ToLowerInvariant();
        var u = await _db.AppUsers.FirstOrDefaultAsync(x => x.Username == name);
        var ok = u != null && new PasswordHasher<AppUser>().VerifyHashedPassword(u, u.PasswordHash, password ?? "") != PasswordVerificationResult.Failed;
        if (!ok) { ViewBag.Error = "اسم المستخدم أو كلمة السر غير صحيحة"; ViewBag.ReturnUrl = returnUrl; return View(); }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.FullName ?? u.Username),
            new(ClaimTypes.Role, u.Role),
            new("region", u.RegionId?.ToString() ?? "")
        };
        await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")));
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync("Cookies"); return RedirectToAction(nameof(Login)); }

    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(string current, string next)
    {
        var u = await _db.AppUsers.FindAsync(int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value));
        var h = new PasswordHasher<AppUser>();
        if (h.VerifyHashedPassword(u, u.PasswordHash, current ?? "") == PasswordVerificationResult.Failed) { ViewBag.Error = "كلمة السر الحالية غير صحيحة"; return View(); }
        if ((next ?? "").Length < 8) { ViewBag.Error = "كلمة السر الجديدة يجب ألا تقل عن 8 أحرف"; return View(); }
        u.PasswordHash = h.HashPassword(u, next);
        await _db.SaveChangesAsync();
        TempData["msg"] = "تم تغيير كلمة السر";
        return RedirectToAction("Index", "Home");
    }
}
