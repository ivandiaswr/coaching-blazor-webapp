using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<List<SubscriptionPlan>> GetAllAsync();
        Task<SubscriptionPlan?> GetByIdAsync(int id);
        Task AddOrUpdateAsync(SubscriptionPlan plan);
        Task DeleteAsync(int id);
    }
}