using ModelLayer.Models.DTOs;

namespace BusinessLayer.Services.Interfaces
{
    public interface IUserSubscriptionService
    {
        Task<bool> RegisterMonthlyUsage(string userId);
        Task<bool> HasActiveSubscription(string userId);
        Task<int> GetRemainingSessionsThisMonth(string userId);
        Task<SubscriptionStatusDto> GetStatusAsync(string userId);
        Task RollbackMonthlyUsage(string userId);
    }
}