namespace BankLiaison.Models;

public enum DayStatus { Unrecorded = 0, Present = 1, Absent = 2, Leave = 3, Mission = 4 }

public class Region { public int Id { get; set; } public string Name { get; set; } public List<Branch> Branches { get; set; } = new(); }
public class Branch { public int Id { get; set; } public string Name { get; set; } public int RegionId { get; set; } public Region Region { get; set; } public List<Employee> Employees { get; set; } = new(); }
public class Employee { public int Id { get; set; } public string FullName { get; set; } public string Phone { get; set; } public int BranchId { get; set; } public Branch Branch { get; set; } public bool IsPrimary { get; set; } public bool IsActive { get; set; } = true; }
public class AppUser { public int Id { get; set; } public string Username { get; set; } public string PasswordHash { get; set; } public string FullName { get; set; } public string Role { get; set; } public int? RegionId { get; set; } public Region Region { get; set; } }
public class AttendanceRecord { public int Id { get; set; } public int EmployeeId { get; set; } public Employee Employee { get; set; } public DateOnly Date { get; set; } public DayStatus Status { get; set; } public string Notes { get; set; } public string RecordedBy { get; set; } }
public class LeaveRecord { public int Id { get; set; } public int EmployeeId { get; set; } public Employee Employee { get; set; } public DateOnly FromDate { get; set; } public DateOnly ToDate { get; set; } public string Type { get; set; } public string Notes { get; set; } public string RecordedBy { get; set; } }

public class BranchRow { public Branch Branch { get; set; } public Employee Primary { get; set; } public Employee Backup { get; set; } public DayStatus PStatus { get; set; } public DayStatus BStatus { get; set; } public string Coverage { get; set; } }
public class DashVm { public DateOnly Date { get; set; } public int? RegionId { get; set; } public List<Region> Regions { get; set; } public List<BranchRow> Rows { get; set; } }
public class DayItem { public int EmployeeId { get; set; } public string Name { get; set; } public string Branch { get; set; } public string Region { get; set; } public bool IsPrimary { get; set; } public DayStatus Status { get; set; } public string Notes { get; set; } }
public class DayVm { public DateOnly Date { get; set; } public int? RegionId { get; set; } public List<Region> Regions { get; set; } public List<DayItem> Items { get; set; } }
public class SaveRow { public int EmployeeId { get; set; } public DayStatus Status { get; set; } public string Notes { get; set; } }
public class SaveVm { public DateOnly Date { get; set; } public int? RegionId { get; set; } public List<SaveRow> Rows { get; set; } }
public class EmpVm { public Employee Employee { get; set; } public List<AttendanceRecord> Records { get; set; } public List<LeaveRecord> Leaves { get; set; } }
public class UsersVm { public List<AppUser> Users { get; set; } public List<Region> Regions { get; set; } }

public static class Ui
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)); // توقيت العراق
    public static string Label(DayStatus s) => s switch { DayStatus.Present => "حاضر", DayStatus.Absent => "غائب", DayStatus.Leave => "إجازة", DayStatus.Mission => "مهمة رسمية", _ => "لم يُسجَّل" };
    public static string Css(DayStatus s) => s switch { DayStatus.Present => "ok", DayStatus.Absent => "bad", DayStatus.Leave => "warn", DayStatus.Mission => "info", _ => "none" };
    public static string CoverageLabel(string c) => c switch { "ok" => "الفرع مغطّى", "covered" => "البديل يغطي", "pending" => "بانتظار التسجيل", _ => "لا يوجد من يغطي" };
    public static string CoverageCss(string c) => c switch { "ok" => "ok", "covered" => "info", "pending" => "none", _ => "bad" };
    public static int CoverageOrder(string c) => c switch { "uncovered" => 0, "pending" => 1, "covered" => 2, _ => 3 };

    static bool Avail(DayStatus s) => s == DayStatus.Present || s == DayStatus.Mission;
    public static string Coverage(Employee p, DayStatus ps, Employee b, DayStatus bs)
    {
        if (p != null && Avail(ps)) return "ok";
        if (p != null && ps == DayStatus.Unrecorded) return "pending";
        if (b != null && Avail(bs)) return "covered";
        if (b != null && bs == DayStatus.Unrecorded) return "pending";
        return "uncovered";
    }
}
