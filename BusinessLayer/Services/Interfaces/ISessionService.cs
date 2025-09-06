using ModelLayer.Models;
using ModelLayer.Models.Enums;

namespace BusinessLayer.Services.Interfaces;

public interface ISessionService
{
    Task<List<Session>> GetAllSessionsAsync(bool includePending = false);
    Task<Session> CreatePendingSessionAsync(Session session);
    Task<Session> GetSessionByStripeSessionIdAsync(string stripeSessionId);
    Task<Session> GetSessionByIdAsync(int id);
    Task<bool> CreateSessionAsync(Session contact);
    Task<bool> CreateSessionWithPackConsumptionAsync(Session session, string userId, string packId, ISessionPackService sessionPackService);
    void UpdateSession(Session session);
    void DeleteSession(int id);
    Task<bool> UpdateScheduledTimeAsync(int sessionId, DateTime newScheduledTime);
    Task<Session?> GetLatestSessionByEmailAsync(string email);
    Task<Session?> GetSessionByEmailAsync(string email);
    Task<Session> GetPendingSessionByEmailAndPackAsync(string email, string packId, SessionType sessionCategory, DateTime preferredDateTime);
    Task CleanupPendingSessionsForUserAsync(string email);

    // Email    
    Task SendEmailAsync(Session contact);
    Task SendSessionConfirmationEmailAsync(Session session, VideoSession videoSession);
}
