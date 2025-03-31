public class UnavailableTime 
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Reason { get; set; } = "Unavailable";
    public bool IsRecurring { get; set; }
}