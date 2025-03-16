using DataAccessLayer;
using ModelLayer.Models;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System;

public class LogProcessor : BaseProcessor<LogRecord>
{
    private readonly IServiceProvider _serviceProvider;

    public LogProcessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void OnEnd(LogRecord logRecord)
    {
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();

            var log = new Log
            {
                LogLevel = logRecord.LogLevel.ToString(),
                Message = logRecord.FormattedMessage,
                Exception = logRecord.Exception?.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            context.Logs.Add(log);
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogProcessor] Failed to log to database: {ex}");
        }
    }
}
