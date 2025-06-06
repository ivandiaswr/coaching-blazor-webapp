using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface ISessionService
{
    Task<List<Session>> GetAllSessionsAsync(bool includePending = false);
    Task CreatePendingSessionAsync(Session session);
    Task<Session> GetSessionByStripeSessionIdAsync(string stripeSessionId);
    Session GetSessionById(int id);
    Task<bool> CreateSessionAsync(Session contact);
    void UpdateSession(Session session);
    void DeleteSession(int id);
    Task<Session?> GetLatestSessionByEmailAsync(string email);
    Task<Session?> GetSessionByEmailAsync(string email);
    Task<Session> GetPendingSessionByEmailAndPackAsync(string email, string packId);

    // Email    
    Task SendEmailAsync(Session contact);
    Task SendSessionConfirmationEmailAsync(Session session, VideoSession videoSession);
}
