using ModelLayer.Models;
using ModelLayer.Models.Enums;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISessionPackPriceService
    {
        Task<List<SessionPackPrice>> GetAllAsync();
        Task<List<SessionPackPrice>> GetPricesForSessionTypeAsync(SessionType sessionType);
        Task<SessionPackPrice?> GetByIdAsync(int id);
        Task AddOrUpdateAsync(SessionPackPrice price);
        Task UpdateMultipleAsync(List<SessionPackPrice> prices);
        Task DeleteAsync(int id);
    }
}