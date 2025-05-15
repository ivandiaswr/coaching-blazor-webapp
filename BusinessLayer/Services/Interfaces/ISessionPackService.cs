using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISessionPackService
    {
        Task<List<SessionPack>> GetAllAsync();
        Task CreateAsync(SessionPack pack);
        Task UpdateAsync(SessionPack pack);
        Task DeleteAsync(int id);
        Task<bool> ConsumeSession(string userId, string packId);
        Task<bool> RollbackSessionConsumption(string userId, string packId);
        Task<int> GetRemainingSessions(string userId);
        Task<List<SessionPack>> GetUserPacksAsync(string userId);
        Task RestoreSession(string userId);
        Task<List<SessionPackPrice>> GetPricesForSessionTypeAsync(SessionType sessionType); 
    }
}