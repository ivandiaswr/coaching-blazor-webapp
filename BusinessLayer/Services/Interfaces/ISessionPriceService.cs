using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISessionPriceService
    {
        Task<List<SessionPrice>> GetAllAsync();
        Task<SessionPrice?> GetBySessionTypeAsync(SessionType type);
        Task AddOrUpdateAsync(SessionPrice price);
        Task DeleteAsync(int id);
    }
}