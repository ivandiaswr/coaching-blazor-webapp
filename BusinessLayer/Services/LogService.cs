using DataAccessLayer;

public class LogService : ILogService
{
    private readonly CoachingDbContext _context;

    public LogService(CoachingDbContext context)
    {
        this._context = context;
    }

    public void LogInfo(string message)
    {
        SaveLog("Information", message);
    }

    public void LogWarning(string message)
    {
        SaveLog("Warning", message);
    }

    public void LogError(string message, string exception)
    {
        SaveLog("Error", message, exception);
    }

    private void SaveLog(string logLevel, string message, string? exception = null)
    {
        var log = new ModelLayer.Models.Log
        {
            LogLevel = logLevel,
            Message = message,
            Exception = exception,
            CreatedAt = DateTime.UtcNow
        };

        _context.Logs.Add(log);
        _context.SaveChanges();
    }
}