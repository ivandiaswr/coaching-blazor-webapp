using BusinessLayer.Services.Interfaces;
using DataAccessLayer;

public class LogService : ILogService
{
    private readonly CoachingDbContext _context;

    public LogService(CoachingDbContext context)
    {
        this._context = context;
    }

    public async Task LogInfo(string message, string exception) 
    {
        await SaveLog("Information", message, exception);
    }

    public async Task LogWarning(string message, string exception) 
    {
        await SaveLog("Warning", message, exception);
    }

    public async Task LogError(string message, string exception)
    {
        await SaveLog("Error", message, exception);
    }

    private async Task SaveLog(string logLevel, string message, string? exception = null)
    {
        var log = new ModelLayer.Models.Log
        {
            LogLevel = logLevel,
            Message = message,
            Exception = exception,
            CreatedAt = DateTime.UtcNow
        };

        _context.Logs.Add(log);
        await _context.SaveChangesAsync();
    }
}