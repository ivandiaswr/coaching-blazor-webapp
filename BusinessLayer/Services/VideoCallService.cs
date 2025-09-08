using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelLayer.Models;

public class VideoCallService : IVideoCallService
{
    private readonly CoachingDbContext _context;
    private readonly ILogger<VideoCallService> _logger;

    public VideoCallService(CoachingDbContext context, ILogger<VideoCallService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VideoSession> CreateSessionAsync(string userId)
    {
        try
        {
            var session = new VideoSession // Creates session id on object creation
            {
                UserId = userId
            };

            _context.Add(session);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Video session created successfully for user {UserId} with session ID {SessionId}",
                userId, session.SessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create video session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<VideoSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            return await _context.VideoSessions
                .Include(s => s.Session)
                .AsNoTracking() // Performance optimization for read-only operations
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> MarkSessionAsStartedAsync(string sessionId)
    {
        try
        {
            var session = await _context.VideoSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

            if (session != null && !session.StartedAt.HasValue)
            {
                session.StartedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video session {SessionId} marked as started", sessionId);
                return true;
            }

            _logger.LogWarning("Unable to mark session {SessionId} as started - session not found or already started", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark session {SessionId} as started", sessionId);
            throw;
        }
    }

    public async Task<bool> EndSessionAsync(string sessionId)
    {
        try
        {
            var session = await _context.VideoSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

            if (session != null)
            {
                session.EndedAt = DateTime.UtcNow;
                session.IsActive = false;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Video session {SessionId} ended successfully", sessionId);
                return true;
            }

            _logger.LogWarning("Unable to end session {SessionId} - session not found", sessionId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<List<VideoSession>> GetSessionsForUserAsync(string userId)
    {
        try
        {
            return await _context.VideoSessions
                .Include(v => v.Session)
                .AsNoTracking()
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.ScheduledAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateScheduledTimeAsync(int videoSessionId, DateTime newScheduledTime)
    {
        try
        {
            var videoSession = await _context.VideoSessions
                .FirstOrDefaultAsync(v => v.Id == videoSessionId);

            if (videoSession != null)
            {
                videoSession.ScheduledAt = newScheduledTime;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated scheduled time for video session {VideoSessionId} to {NewTime}",
                    videoSessionId, newScheduledTime);
                return true;
            }

            _logger.LogWarning("Video session {VideoSessionId} not found for schedule update", videoSessionId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update scheduled time for video session {VideoSessionId}", videoSessionId);
            throw;
        }
    }

    public async Task<bool> IsSessionValidAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            return session != null && session.IsActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<TimeSpan?> GetSessionDurationAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session?.StartedAt.HasValue == true && session.EndedAt.HasValue)
            {
                return session.EndedAt.Value - session.StartedAt.Value;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate session duration for {SessionId}", sessionId);
            return null;
        }
    }
}
