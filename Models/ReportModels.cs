namespace BankLiaison.Models;

public class ReportVm
{
    public Employee Employee { get; set; }
    public string Period { get; set; }
    public string Title { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly Prev { get; set; }
    public DateOnly Next { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<int[]> Series { get; set; } = new(); // الترتيب: حاضر، غائب، إجازة، مهمة، لم يُسجَّل
    public int[] Totals { get; set; } = new int[5];
    public int Rate { get; set; }
}
