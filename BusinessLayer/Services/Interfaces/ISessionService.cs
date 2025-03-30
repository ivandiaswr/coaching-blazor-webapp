using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface ISessionService
{
    List<Session> GetAllSessions();
    Session GetSessionById(int id);
    Task<bool> CreateSessionAsync(Session contact);
    void UpdateSession(Session session);
    void DeleteSession(int id);

    // Email    
    Task SendEmailAsync(Session contact);
}
