using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface ISessionService
{
    Task<List<Session>> GetAllSessionsAsync();
    Session GetSessionById(int id);
    Task<bool> CreateSessionAsync(Session contact);
    void UpdateSession(Session session);
    void DeleteSession(int id);
    Task<Session?> GetLatestSessionByEmailAsync(string email);

    // Email    
    Task SendEmailAsync(Session contact);
    Task SendSessionConfirmationEmailAsync(Session session, VideoSession videoSession);
}
