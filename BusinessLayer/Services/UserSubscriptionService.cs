using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModelLayer.Models;
using ModelLayer.Models.DTOs;
using Stripe;

namespace BusinessLayer.Services;

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
                .Include(s => s.Price)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Price == null)
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

            if (subscription.SessionsUsedThisMonth >= subscription.Price.MonthlyLimit)
            {
                await _logService.LogInfo("RegisterMonthlyUsage", $"Monthly limit reached for user: {userId}");
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
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

        if (subscription == null || subscription.Price == null)
            return 0;

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        if (subscription.CurrentPeriodStart < startOfMonth)
        {
            subscription.SessionsUsedThisMonth = 0;
            subscription.CurrentPeriodStart = startOfMonth;
            await _context.SaveChangesAsync();
        }

        return Math.Max(0, subscription.Price.MonthlyLimit - subscription.SessionsUsedThisMonth);
    }

    public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
    {
        try
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Price)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Price == null)
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

            if (stripeSubscription.Items?.Data?.Any() == true)
            {
                var firstItem = stripeSubscription.Items.Data[0];
                subscription.CurrentPeriodStart = firstItem.CurrentPeriodStart;
                subscription.CurrentPeriodEnd = firstItem.CurrentPeriodEnd;
                await _context.SaveChangesAsync();
            }
            else
            {
                await _logService.LogError("GetStatusAsync", $"No items found for StripeSubscriptionId: {subscription.StripeSubscriptionId}");
                return new SubscriptionStatusDto
                {
                    HasActiveSubscription = false,
                    MonthlyLimit = 0,
                    SessionsUsed = 0,
                    PlanId = subscription.PriceId.ToString()
                };
            }

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            if (subscription.CurrentPeriodStart < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.CurrentPeriodStart = startOfMonth;
                await _context.SaveChangesAsync();
            }

            var monthlyLimit = subscription.Price.MonthlyLimit;
            var sessionsUsed = subscription.SessionsUsedThisMonth;
            var remaining = Math.Max(0, monthlyLimit - sessionsUsed);

            return new SubscriptionStatusDto
            {
                HasActiveSubscription = true,
                MonthlyLimit = monthlyLimit,
                SessionsUsed = sessionsUsed,
                PlanId = subscription.PriceId.ToString()
            };
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
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

        if (subscription == null || subscription.Price == null)
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
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<bool> CancelSubscriptionAsync(string userId)
    {
        try
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Price)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (subscription == null || subscription.Price == null)
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

                if (updatedSubscription.Items?.Data?.Any() == true)
                {
                    var firstItem = updatedSubscription.Items.Data[0];
                    subscription.CurrentPeriodEnd = firstItem.CurrentPeriodEnd;
                }

                await _context.SaveChangesAsync();
                await _logService.LogInfo("CancelSubscriptionAsync", $"Successfully canceled subscription for user: {userId}");
                return true;
            }

            await _logService.LogError("CancelSubscriptionAsync", $"Failed to cancel StripeSubscriptionId: {subscription.StripeSubscriptionId}");
            return false;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CancelSubscriptionAsync Error", ex.Message);
            return false;
        }
    }
}