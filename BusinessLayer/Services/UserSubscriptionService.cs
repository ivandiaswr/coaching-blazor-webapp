using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using ModelLayer.Models.DTOs;

namespace BusinessLayer.Services
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly CoachingDbContext _context;

        public UserSubscriptionService(CoachingDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RegisterMonthlyUsage(string userId)
        {
            var now = DateTime.UtcNow;
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Plan == null)
                return false;

            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            if (subscription.StartedAt < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.StartedAt = startOfMonth;
            }

            if (subscription.SessionsUsedThisMonth >= subscription.Plan.MonthlySessionLimit)
                return false;

            subscription.SessionsUsedThisMonth++;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasActiveSubscription(string userId)
        {
            return await _context.UserSubscriptions
                .Include(s => s.Plan)
                .AnyAsync(s => s.UserId == userId && s.IsActive);
        }

        public async Task<int> GetRemainingSessionsThisMonth(string userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Plan == null)
                return 0;

            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            if (subscription.StartedAt < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.StartedAt = startOfMonth;
                await _context.SaveChangesAsync();
            }

            return subscription.Plan.MonthlySessionLimit - subscription.SessionsUsedThisMonth;
        }

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Plan == null)
            {
                return new SubscriptionStatusDto
                {
                    HasActiveSubscription = false
                };
            }

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            if (subscription.StartedAt < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.StartedAt = startOfMonth;
                await _context.SaveChangesAsync();
            }

            return new SubscriptionStatusDto
            {
                HasActiveSubscription = true,
                MonthlyLimit = subscription.Plan.MonthlySessionLimit,
                SessionsUsed = subscription.SessionsUsedThisMonth
            };
        }

        public async Task RollbackMonthlyUsage(string userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Plan == null)
                return;

            if (subscription.SessionsUsedThisMonth > 0)
            {
                subscription.SessionsUsedThisMonth--;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<UserSubscription?> GetActiveSubscriptionAsync(string userId)
        {
            return await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
        }
    }
}