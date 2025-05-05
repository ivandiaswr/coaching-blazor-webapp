using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModelLayer.Models;
using ModelLayer.Models.DTOs;
using Stripe;

namespace BusinessLayer.Services
{
    public class UserSubscriptionService : IUserSubscriptionService
    {
        private readonly IConfiguration _configuration;
        private readonly CoachingDbContext _context;
        private readonly ILogService _logService;
        private readonly IHelperService _helperService;

        public UserSubscriptionService(
            IConfiguration configuration,
            CoachingDbContext context,
            ILogService logService,
            IHelperService helperService)
        {
            _configuration = configuration;
            _context = context;
            _logService = logService;
            _helperService = helperService;
            StripeConfiguration.ApiKey = _helperService.GetConfigValue("Stripe:SecretKey");
        }

        public async Task<bool> RegisterMonthlyUsage(string userId)
        {
            try
            {
                var subscription = await _context.UserSubscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

                if (subscription == null || subscription.Plan == null)
                {
                    await _logService.LogError("RegisterMonthlyUsage", $"No active subscription found for user: {userId}");
                    return false;
                }

                var now = DateTime.UtcNow;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                if (subscription.CurrentPeriodStart < startOfMonth)
                {
                    subscription.SessionsUsedThisMonth = 0;
                    subscription.CurrentPeriodStart = startOfMonth;
                    await _context.SaveChangesAsync();
                }

                if (subscription.SessionsUsedThisMonth >= subscription.Plan.MonthlySessionLimit)
                {
                    await _logService.LogInfo($"RegisterMonthlyUsage -> Monthly limit reached for user: {userId}");
                    return false;
                }

                subscription.SessionsUsedThisMonth++;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logService.LogError("RegisterMonthlyUsage Error", ex.Message);
                return false;
            }
        }

        public async Task<bool> HasActiveSubscription(string userId)
        {
            return await _context.UserSubscriptions
                .AnyAsync(s => s.UserId == userId && s.IsActive);
        }

        public async Task<int> GetRemainingSessionsThisMonth(string userId)
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Plan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Plan == null)
                return 0;

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            if (subscription.CurrentPeriodStart < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.CurrentPeriodStart = startOfMonth;
                await _context.SaveChangesAsync();
            }

            return Math.Max(0, subscription.Plan.MonthlySessionLimit - subscription.SessionsUsedThisMonth);
        }

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            try
            {
                var subscription = await _context.UserSubscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

                if (subscription == null || subscription.Plan == null)
                {
                    return new SubscriptionStatusDto
                    {
                        HasActiveSubscription = false,
                        MonthlyLimit = 0,
                        SessionsUsed = 0,
                        PlanId = null
                    };
                }

                var stripeService = new SubscriptionService();
                var stripeSubscription = await stripeService.GetAsync(subscription.StripeSubscriptionId);

                // Verify if has itens in subscription
                if (stripeSubscription.Items?.Data?.Any() == true)
                {
                    var firstItem = stripeSubscription.Items.Data[0];

                    subscription.CurrentPeriodStart = firstItem.CurrentPeriodStart;
                    subscription.CurrentPeriodEnd = firstItem.CurrentPeriodEnd;

                    await _context.SaveChangesAsync();
                }
                else
                {
                    await _logService.LogError("GetStatusAsync", $"A subscrição com ID {subscription.StripeSubscriptionId} não possui itens.");
                    return new SubscriptionStatusDto
                    {
                        HasActiveSubscription = false,
                        MonthlyLimit = 0,
                        SessionsUsed = 0,
                        PlanId = subscription.SubscriptionPlanId.ToString()
                    };
                }

                // Reset sessions if new billing period
                var now = DateTime.UtcNow;
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                if (subscription.CurrentPeriodStart < startOfMonth)
                {
                    subscription.SessionsUsedThisMonth = 0;
                    subscription.CurrentPeriodStart = startOfMonth;
                    await _context.SaveChangesAsync();
                }

                var monthlyLimit = subscription.Plan.MonthlySessionLimit;
                var sessionsUsed = subscription.SessionsUsedThisMonth;
                var remaining = Math.Max(0, monthlyLimit - sessionsUsed);

                return new SubscriptionStatusDto
                {
                    HasActiveSubscription = true,
                    MonthlyLimit = monthlyLimit,
                    SessionsUsed = sessionsUsed,
                    PlanId = subscription.SubscriptionPlanId.ToString()
                };
            }
            catch (StripeException ex)
            {
                await _logService.LogError("GetStatusAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
                throw;
            }
            catch (Exception ex)
            {
                await _logService.LogError("GetStatusAsync Error", ex.Message);
                throw;
            }
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

        public async Task<bool> CancelSubscriptionAsync(string userId)
        {
            try
            {
                var subscription = await _context.UserSubscriptions
                    .Include(s => s.Plan)
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

                if (subscription == null || subscription.Plan == null)
                {
                    await _logService.LogError("CancelSubscriptionAsync", $"No active subscription found for user: {userId}");
                    return false;
                }

                var stripeService = new SubscriptionService();
                var options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true
                };
                var updatedSubscription = await stripeService.UpdateAsync(subscription.StripeSubscriptionId, options);

                if (updatedSubscription.CancelAtPeriodEnd)
                {
                    subscription.IsActive = false;
                    subscription.CancelledAt = DateTime.UtcNow;

                    // Get dates from first subscription item
                    if (updatedSubscription.Items?.Data?.Any() == true)
                    {
                        var firstItem = updatedSubscription.Items.Data[0];
                        subscription.CurrentPeriodEnd = firstItem.CurrentPeriodEnd;
                    }
                    else
                    {
                        await _logService.LogWarning($"CancelSubscriptionAsync -> No subscription items found for StripeSubscriptionId: {subscription.StripeSubscriptionId}");
                    }

                    await _context.SaveChangesAsync();

                    await _logService.LogInfo($"CancelSubscriptionAsync -> Successfully canceled subscription for user: {userId}, StripeSubscriptionId: {subscription.StripeSubscriptionId}");
                    return true;
                }
                else
                {
                    await _logService.LogError("CancelSubscriptionAsync", $"Failed to set cancel_at_period_end for StripeSubscriptionId: {subscription.StripeSubscriptionId}");
                    return false;
                }
            }
            catch (StripeException ex)
            {
                await _logService.LogError("CancelSubscriptionAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
                return false;
            }
            catch (Exception ex)
            {
                await _logService.LogError("CancelSubscriptionAsync Error", ex.Message);
                return false;
            }
        }
    }
}