using ModelLayer.Models;

public interface IVideoCallService 
{
    Task<VideoSession> CreateSessionAsync(string userId);
    Task<VideoSession?> GetSessionAsync(string sessionId);
    Task<List<VideoSession>> GetSessionsForUserAsync(string userId);
    Task EndSessionAsync(string sessionId);
}