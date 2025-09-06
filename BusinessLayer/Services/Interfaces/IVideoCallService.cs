using ModelLayer.Models;

public interface IVideoCallService
{
    Task<VideoSession> CreateSessionAsync(string userId);
    Task<VideoSession?> GetSessionAsync(string sessionId);
    Task<List<VideoSession>> GetSessionsForUserAsync(string userId);
    Task<bool> MarkSessionAsStartedAsync(string sessionId);
    Task EndSessionAsync(string sessionId);
    Task<bool> UpdateScheduledTimeAsync(int videoSessionId, DateTime newScheduledTime);
}