using DataAccessLayer;
using ModelLayer.Models;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;

public class LogProcessor : BaseProcessor<LogRecord>
{
    private readonly IServiceProvider _serviceProvider;

    public LogProcessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void OnEnd(LogRecord logRecord)
    {
        
        Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
                var log = new Log
                {
                    LogLevel = logRecord.LogLevel.ToString(),
                    Message = logRecord.FormattedMessage ?? logRecord.Body,
                    Exception = logRecord.Exception?.ToString(),
                    CreatedAt = DateTime.UtcNow
                };
                context.Logs.Add(log);
                await context.SaveChangesAsync();
                Console.WriteLine($"[LogProcessor] Saved: {log.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogProcessor] Error: {ex.Message}");
            }
        });
    }
}
