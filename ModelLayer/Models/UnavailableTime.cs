using System.ComponentModel.DataAnnotations;

public class UnavailableTime
{
    public int Id { get; set; }

    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Day of the week is required")]
    public DayOfWeek? DayOfWeek { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    public TimeSpan? StartTime { get; set; }

    [Required(ErrorMessage = "End time is required")]
    public TimeSpan? EndTime { get; set; }

    [MaxLength(100)]
    public string Reason { get; set; } = "Unavailable";

    public bool IsRecurring { get; set; } = false;
}
