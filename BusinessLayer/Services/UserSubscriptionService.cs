using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using ModelLayer.Models.DTOs;
using ModelLayer.Models.Enums;
using Stripe;

namespace BusinessLayer.Services;

public class UserSubscriptionService : IUserSubscriptionService
{
    private readonly CoachingDbContext _context;
    private readonly ILogService _logService;
    private readonly IHelperService _helperService;

    public UserSubscriptionService(
        CoachingDbContext context,
        ILogService logService,
        IHelperService helperService)
    {
        _context = context;
        _logService = logService;
        _helperService = helperService;
        StripeConfiguration.ApiKey = _helperService.GetConfigValue("Stripe:SecretKey");
    }

    public async Task<bool> RegisterMonthlyUsage(string userId, string subscriptionId)
    {
        try
        {
            await _logService.LogInfo("RegisterMonthlyUsage",
                $"Called with UserId={userId}, SubscriptionId={subscriptionId}");

            var subscription = await _context.UserSubscriptions
                .Include(s => s.Price)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive && s.StripeSubscriptionId == subscriptionId);

            if (subscription == null || subscription.Price == null)
            {
                await _logService.LogError("RegisterMonthlyUsage",
                    $"No active subscription found for user: {userId}, SubscriptionId: {subscriptionId}");
                return false;
            }

            await _logService.LogInfo("RegisterMonthlyUsage",
                $"Before update: Id={subscription.Id}, SessionsUsedThisMonth={subscription.SessionsUsedThisMonth}, CurrentPeriodStart={subscription.CurrentPeriodStart:yyyy-MM-dd HH:mm:ss.fff zzz}, CurrentPeriodEnd={subscription.CurrentPeriodEnd:yyyy-MM-dd HH:mm:ss.fff zzz}");

            // Use WEST time zone
            var westTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, westTimeZone);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            // Check if the subscription needs a reset (current month is after CurrentPeriodStart's month)
            if (subscription.CurrentPeriodStart < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.CurrentPeriodStart = startOfMonth;
                subscription.CurrentPeriodEnd = endOfMonth;
                _context.Entry(subscription).State = EntityState.Modified;
                await _logService.LogInfo("RegisterMonthlyUsage",
                    $"Reset SessionsUsedThisMonth to 0 and updated period for SubscriptionId: {subscription.StripeSubscriptionId}, UserId: {userId}, New CurrentPeriodStart={startOfMonth:yyyy-MM-dd}, New CurrentPeriodEnd={endOfMonth:yyyy-MM-dd HH:mm:ss.fff}");
                await _context.SaveChangesAsync();
            }

            if (subscription.SessionsUsedThisMonth >= subscription.Price.MonthlyLimit)
            {
                await _logService.LogInfo("RegisterMonthlyUsage",
                    $"Monthly limit reached for user: {userId}, SubscriptionId: {subscriptionId}");
                return false;
            }

            subscription.SessionsUsedThisMonth++;
            _context.Entry(subscription).State = EntityState.Modified;
            var rowsAffected = await _context.SaveChangesAsync();
            await _logService.LogInfo("RegisterMonthlyUsage",
                $"Increment session: Id={subscription.Id}, SessionsUsedThisMonth={subscription.SessionsUsedThisMonth}, Rows affected={rowsAffected}");

            if (rowsAffected == 0)
            {
                await _logService.LogError("RegisterMonthlyUsage",
                    $"No rows affected for user: {userId}, SubscriptionId: {subscriptionId}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogError("RegisterMonthlyUsage Error",
                $"Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
            return false;
        }
    }

    public async Task<bool> HasActiveSubscription(string userId)
    {
        return await _context.UserSubscriptions
            .AnyAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<int> GetRemainingSessionsThisMonth(string userId, string? subscriptionId = null)
    {
        var query = _context.UserSubscriptions
            .Include(s => s.Price)
            .Where(s => s.UserId == userId && s.IsActive);

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            query = query.Where(s => s.StripeSubscriptionId == subscriptionId);
        }

        var subscription = await query.FirstOrDefaultAsync();
        if (subscription == null || subscription.Price == null)
            return 0;

        var westTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, westTimeZone);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59);

        if (subscription.CurrentPeriodStart < startOfMonth)
        {
            subscription.SessionsUsedThisMonth = 0;
            subscription.CurrentPeriodStart = startOfMonth;
            subscription.CurrentPeriodEnd = endOfMonth;
            _context.Entry(subscription).State = EntityState.Modified;
            await _logService.LogInfo("GetRemainingSessionsThisMonth",
                $"Reset SessionsUsedThisMonth to 0 and updated period for SubscriptionId: {subscription.StripeSubscriptionId}, UserId: {userId}, New CurrentPeriodStart={startOfMonth:yyyy-MM-dd}, New CurrentPeriodEnd={endOfMonth:yyyy-MM-dd HH:mm:ss.fff}");
            await _context.SaveChangesAsync();
        }

        return Math.Max(0, subscription.Price.MonthlyLimit - subscription.SessionsUsedThisMonth);
    }

    public async Task<Dictionary<SessionType, SubscriptionStatusDto>> GetStatusAsync(string userId)
    {
        var subscriptions = await GetActiveSubscriptionsAsync(userId);
        var result = new Dictionary<SessionType, SubscriptionStatusDto>();

        // Use WEST time zone
        var westTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, westTimeZone);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59);

        foreach (var subscription in subscriptions)
        {
            if (subscription.Price == null) continue;

            // Check if the subscription needs a reset
            if (subscription.CurrentPeriodStart < startOfMonth)
            {
                subscription.SessionsUsedThisMonth = 0;
                subscription.CurrentPeriodStart = startOfMonth;
                subscription.CurrentPeriodEnd = endOfMonth;
                _context.Entry(subscription).State = EntityState.Modified;
                await _logService.LogInfo("GetStatusAsync",
                    $"Reset SessionsUsedThisMonth to 0 and updated period for SubscriptionId: {subscription.StripeSubscriptionId}, UserId: {userId}, New CurrentPeriodStart={startOfMonth:yyyy-MM-dd}, New CurrentPeriodEnd={endOfMonth:yyyy-MM-dd HH:mm:ss.fff}");
            }

            var monthlyLimit = subscription.Price.MonthlyLimit;
            var sessionsUsed = subscription.SessionsUsedThisMonth;
            var remaining = Math.Max(0, monthlyLimit - sessionsUsed);

            result[subscription.Price.SessionType] = new SubscriptionStatusDto
            {
                HasActiveSubscription = true,
                MonthlyLimit = monthlyLimit,
                SessionsUsed = sessionsUsed,
                PlanId = subscription.PriceId.ToString()
            };
        }

        var rowsAffected = await _context.SaveChangesAsync();
        await _logService.LogInfo("GetStatusAsync",
            $"SaveChangesAsync: Rows affected={rowsAffected}, UserId={userId}");

        if (!result.Any())
        {
            result[SessionType.lifeCoaching] = new SubscriptionStatusDto
            {
                HasActiveSubscription = false,
                MonthlyLimit = 0,
                SessionsUsed = 0,
                PlanId = null
            };
        }

        return result;
    }

    public async Task RollbackMonthlyUsage(string userId, string subscriptionId)
    {
        var subscription = await _context.UserSubscriptions
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive && s.StripeSubscriptionId == subscriptionId);

        if (subscription == null || subscription.Price == null)
            return;

        if (subscription.SessionsUsedThisMonth > 0)
        {
            subscription.SessionsUsedThisMonth--;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<UserSubscription>> GetActiveSubscriptionsAsync(string userId)
    {
        return await _context.UserSubscriptions
            .Include(s => s.Price)
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();
    }

    public async Task<bool> CancelSubscriptionAsync(string userId, string subscriptionId)
    {
        try
        {
            var subscription = await _context.UserSubscriptions
                .Include(s => s.Price)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive && s.StripeSubscriptionId == subscriptionId);

            if (subscription == null || subscription.Price == null)
            {
                await _logService.LogError("CancelSubscriptionAsync", $"No active subscription found for user: {userId}, SubscriptionId: {subscriptionId}");
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
                // Use WEST time zone for CurrentPeriodEnd
                var westTimeZone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, westTimeZone);
                subscription.CurrentPeriodEnd = new DateTime(now.Year, now.Month, 1)
                    .AddMonths(1).AddDays(-1).Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                await _context.SaveChangesAsync();
                await _logService.LogInfo("CancelSubscriptionAsync", $"Successfully canceled subscription for user: {userId}, SubscriptionId: {subscriptionId}");
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