using ModelLayer.Models;
using ModelLayer.Models.Enums;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISessionPriceService
    {
        Task<List<SessionPrice>> GetAllAsync();
        Task<SessionPrice?> GetBySessionTypeAsync(SessionType type);
        Task AddOrUpdateAsync(SessionPrice price);
        Task DeleteAsync(int id);
        Task<SessionPrice?> GetPriceForSessionTypeAsync(SessionType sessionType);
    }
}