using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface ISessionService
{
    Task<List<Session>> GetAllSessionsAsync(bool includePending = false);
    Task<Session> CreatePendingSessionAsync(Session session);
    Task<Session> GetSessionByStripeSessionIdAsync(string stripeSessionId);
    Task<Session> GetSessionByIdAsync(int id);
    Task<bool> CreateSessionAsync(Session contact);
    void UpdateSession(Session session);
    void DeleteSession(int id);
    Task<Session?> GetLatestSessionByEmailAsync(string email);
    Task<Session?> GetSessionByEmailAsync(string email);
    Task<Session> GetPendingSessionByEmailAndPackAsync(string email, string packId, SessionType sessionCategory, DateTime preferredDateTime);

    // Email    
    Task SendEmailAsync(Session contact);
    Task SendSessionConfirmationEmailAsync(Session session, VideoSession videoSession);
}
