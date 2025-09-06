using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

public class VideoCallService : IVideoCallService
{
    private readonly CoachingDbContext _context;

    public VideoCallService(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<VideoSession> CreateSessionAsync(string userId)
    {
        var session = new VideoSession // Creates session id on object creation
        {
            UserId = userId
        };

        _context.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<VideoSession?> GetSessionAsync(string sessionId)
    {
        return await _context.VideoSessions
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);
    }

    public async Task<bool> MarkSessionAsStartedAsync(string sessionId)
    {
        var session = await _context.VideoSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId && s.IsActive);

        if (session != null && !session.StartedAt.HasValue)
        {
            session.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task EndSessionAsync(string sessionId)
    {
        var session = GetSessionAsync(sessionId).Result;

        if (session is not null)
        {
            session.EndedAt = DateTime.UtcNow;
            session.IsActive = false;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<VideoSession>> GetSessionsForUserAsync(string userId)
    {
        return await _context.VideoSessions
            .Include(v => v.Session)
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.ScheduledAt)
            .ToListAsync();
    }

    public async Task<bool> UpdateScheduledTimeAsync(int videoSessionId, DateTime newScheduledTime)
    {
        var videoSession = await _context.VideoSessions
            .FirstOrDefaultAsync(v => v.Id == videoSessionId);

        if (videoSession != null)
        {
            videoSession.ScheduledAt = newScheduledTime;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }
}
