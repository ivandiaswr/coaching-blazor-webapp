using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISubscriptionPriceService
    {
        Task<List<SubscriptionPrice>> GetAllAsync();
        Task<List<SubscriptionPrice>> GetPricesForSessionTypeAsync(SessionType sessionType);
        Task<SubscriptionPrice?> GetByIdAsync(int id);
        Task AddOrUpdateAsync(SubscriptionPrice price);
        Task DeleteAsync(int id);
    }
}