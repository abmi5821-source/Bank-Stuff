using BankLiaison.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankLiaison.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> o) : base(o) { }
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AttendanceRecord> Attendance => Set<AttendanceRecord>();
    public DbSet<LeaveRecord> Leaves => Set<LeaveRecord>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        m.Entity<AttendanceRecord>().HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
        m.Entity<Branch>().HasIndex(b => new { b.RegionId, b.Name }).IsUnique();
    }
}

public static class Seeder
{
    public static void Run(AppDb db)
    {
        db.Database.EnsureCreated();
        if (!db.Regions.Any())
        {
            db.Regions.AddRange(new[] { "المنطقة الشمالية", "المنطقة الوسطى", "المنطقة الجنوبية", "بغداد", "ديالى", "الأنبار" }.Select(n => new Region { Name = n }));
            db.SaveChanges();
        }
        if (!db.AppUsers.Any(u => u.Role == "Admin"))
        {
            var u = new AppUser { Username = "admin", FullName = "مسؤول القسم", Role = "Admin" };
            u.PasswordHash = new PasswordHasher<AppUser>().HashPassword(u, Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "ChangeMe@123");
            db.AppUsers.Add(u);
            db.SaveChanges();
        }
    }

    /// الحالة الفعلية لكل موظف في يوم معيّن: سجل الدوام أولاً، ثم الإجازة المسجلة، وإلا "لم يُسجَّل"
    public static async Task<Dictionary<int, (DayStatus Status, string Note)>> Map(AppDb db, IEnumerable<int> empIds, DateOnly d)
    {
        var ids = empIds.ToList();
        var att = await db.Attendance.Where(a => a.Date == d && ids.Contains(a.EmployeeId)).ToListAsync();
        var leaves = await db.Leaves.Where(l => l.FromDate <= d && l.ToDate >= d && ids.Contains(l.EmployeeId)).ToListAsync();
        var map = new Dictionary<int, (DayStatus, string)>();
        foreach (var id in ids)
        {
            var a = att.FirstOrDefault(x => x.EmployeeId == id);
            var l = leaves.FirstOrDefault(x => x.EmployeeId == id);
            if (a != null) map[id] = (a.Status, a.Notes);
            else if (l != null) map[id] = (DayStatus.Leave, l.Type);
            else map[id] = (DayStatus.Unrecorded, "");
        }
        return map;
    }
}
