using ModelLayer.Models;
using ModelLayer.Models.DTOs;

namespace BusinessLayer.Services.Interfaces
{
    public interface IUserSubscriptionService
    {
        Task<bool> RegisterMonthlyUsage(string userId, string subscriptionId);
        Task<bool> HasActiveSubscription(string userId);
        Task<Dictionary<SessionType, SubscriptionStatusDto>> GetStatusAsync(string userId);
         Task<int> GetRemainingSessionsThisMonth(string userId, string? subscriptionId = null);
        Task<List<UserSubscription>> GetActiveSubscriptionsAsync(string userId);
        Task RollbackMonthlyUsage(string userId, string subscriptionId);
        Task<bool> CancelSubscriptionAsync(string userId, string subscriptionId);
    }
}