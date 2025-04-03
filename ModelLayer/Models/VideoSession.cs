namespace ModelLayer.Models;

public class VideoSession
{
    public int Id { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public DateTime CreatedAt { get; set; } 
    public DateTime ScheduledAt { get; set; } 
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }

    public int? SessionRefId { get; set; }
    public Session? Session { get; set; }
}