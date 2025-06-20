using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using ModelLayer.Models.Enums;
using Stripe;

namespace BusinessLayer.Services
{
    public class SubscriptionPriceService : ISubscriptionPriceService
    {
        private readonly CoachingDbContext _context;
        private readonly IPaymentService _stripeService;
        private readonly ILogService _logService;

        public SubscriptionPriceService(CoachingDbContext context, ILogService logService, IPaymentService stripeService)
        {
            _context = context;
            _logService = logService;
            _stripeService = stripeService;
        }

        public async Task<List<SubscriptionPrice>> GetAllAsync()
        {
            try
            {
                await _logService.LogInfo("SubscriptionPriceService.GetAllAsync", "Fetching all subscription prices");
                return await _context.SubscriptionPrices.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SubscriptionPriceService.GetAllAsync Error", ex.Message);
                throw;
            }
        }

        public async Task<List<SubscriptionPrice>> GetPricesForSessionTypeAsync(SessionType sessionType)
        {
            try
            {
                await _logService.LogInfo("SubscriptionPriceService.GetPricesForSessionTypeAsync", $"Fetching subscription prices for SessionType: {sessionType}");
                return await _context.SubscriptionPrices
                    .Where(p => p.SessionType == sessionType)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SubscriptionPriceService.GetPricesForSessionTypeAsync Error", ex.Message);
                throw;
            }
        }

        public async Task<SubscriptionPrice?> GetByIdAsync(int id)
        {
            try
            {
                await _logService.LogInfo("SubscriptionPriceService.GetByIdAsync", $"Fetching subscription price with ID: {id}");
                return await _context.SubscriptionPrices.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _logService.LogError("SubscriptionPriceService.GetByIdAsync Error", ex.Message);
                throw;
            }
        }

        public async Task AddOrUpdateAsync(SubscriptionPrice subscription)
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription), "Subscription price cannot be null.");

            try
            {
                await _logService.LogInfo("SubscriptionPriceService.AddOrUpdateAsync",
                    $"Processing subscription price for SessionType: {subscription.SessionType}, ID: {subscription.Id}, Name: {subscription.Name}, Price: {subscription.PriceGBP}, MonthlyLimit: {subscription.MonthlyLimit}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var existingPrice = await FindExistingSubscriptionAsync(subscription.Id, subscription.SessionType, subscription.PriceGBP, subscription.StripePriceId);
                    string stripePriceId = existingPrice?.StripePriceId ?? subscription.StripePriceId;

                    if (subscription.PriceGBP > 0)
                    {
                        stripePriceId = await _stripeService.CreateOrUpdateSubscriptionPriceAsync(
                            subscription.Name ?? $"Subscription {subscription.SessionType}",
                            subscription.PriceGBP,
                            subscription.SessionType.ToString(),
                            "GBP");
                    }
                    else
                    {
                        stripePriceId = null;
                    }

                    if (existingPrice != null)
                    {
                        existingPrice.Name = subscription.Name ?? string.Empty;
                        existingPrice.Description = subscription.Description ?? string.Empty;
                        existingPrice.MonthlyLimit = subscription.MonthlyLimit;
                        existingPrice.PriceGBP = subscription.PriceGBP;
                        existingPrice.SessionType = subscription.SessionType;
                        existingPrice.StripePriceId = stripePriceId;
                        existingPrice.LastUpdated = DateTime.UtcNow;

                        _context.SubscriptionPrices.Update(existingPrice);
                        await _logService.LogInfo("AddOrUpdateAsync",
                            $"Updated subscription price with ID: {existingPrice.Id}, Name: {existingPrice.Name}, StripePriceId: {stripePriceId}");
                    }
                    else if (subscription.PriceGBP > 0 && string.IsNullOrEmpty(subscription.StripePriceId))
                    {
                        subscription.StripePriceId = stripePriceId;
                        subscription.LastUpdated = DateTime.UtcNow;
                        _context.SubscriptionPrices.Add(subscription);
                        await _logService.LogInfo("AddOrUpdateAsync",
                            $"Added new subscription price with Name: {subscription.Name}, StripePriceId: {stripePriceId}");
                    }
                    else
                    {
                        await _logService.LogWarning("AddOrUpdateAsync",
                            $"Skipped processing subscription with Name: {subscription.Name} because PriceGBP <= 0 or StripePriceId exists");
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    await _logService.LogError("AddOrUpdateAsync",
                        $"Transaction failed for subscription ID: {subscription.Id}, Name: {subscription.Name}. Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}");
                    throw new Exception($"Failed to save subscription: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("AddOrUpdateAsync",
                    $"Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task UpdateMultipleAsync(List<SubscriptionPrice> subscriptions)
        {
            if (subscriptions == null || !subscriptions.Any()) throw new ArgumentNullException(nameof(subscriptions), "Subscription prices cannot be null or empty.");

            try
            {
                await _logService.LogInfo("SubscriptionPriceService.UpdateMultipleAsync",
                    $"Processing {subscriptions.Count} subscription prices for SessionType: {subscriptions.FirstOrDefault()?.SessionType}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var subscription in subscriptions)
                    {
                        var existingPrice = await FindExistingSubscriptionAsync(subscription.Id, subscription.SessionType, subscription.PriceGBP, subscription.StripePriceId);
                        string stripePriceId = existingPrice?.StripePriceId ?? subscription.StripePriceId;

                        if (subscription.PriceGBP > 0)
                        {
                            stripePriceId = await _stripeService.CreateOrUpdateSubscriptionPriceAsync(
                                subscription.Name ?? $"Subscription {subscription.SessionType}",
                                subscription.PriceGBP,
                                subscription.SessionType.ToString(),
                                "GBP");
                        }
                        else
                        {
                            stripePriceId = null;
                        }

                        if (existingPrice != null)
                        {
                            existingPrice.Name = subscription.Name ?? string.Empty;
                            existingPrice.Description = subscription.Description ?? string.Empty;
                            existingPrice.MonthlyLimit = subscription.MonthlyLimit;
                            existingPrice.PriceGBP = subscription.PriceGBP;
                            existingPrice.SessionType = subscription.SessionType;
                            existingPrice.StripePriceId = stripePriceId;
                            existingPrice.LastUpdated = DateTime.UtcNow;

                            _context.SubscriptionPrices.Update(existingPrice);
                            await _logService.LogInfo("UpdateMultipleAsync",
                                $"Updated subscription price with ID: {existingPrice.Id}, Name: {existingPrice.Name}, StripePriceId: {stripePriceId}");
                        }
                        else if (subscription.PriceGBP > 0 && string.IsNullOrEmpty(subscription.StripePriceId))
                        {
                            subscription.StripePriceId = stripePriceId;
                            subscription.LastUpdated = DateTime.UtcNow;
                            _context.SubscriptionPrices.Add(subscription);
                            await _logService.LogInfo("UpdateMultipleAsync",
                                $"Added new subscription price with Name: {subscription.Name}, StripePriceId: {stripePriceId}");
                        }
                        else
                        {
                            await _logService.LogWarning("UpdateMultipleAsync",
                                $"Skipped processing subscription with Name: {subscription.Name} because PriceGBP <= 0 or StripePriceId exists");
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    await _logService.LogError("UpdateMultipleAsync",
                        $"Transaction failed for subscription prices. Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}");
                    throw new Exception($"Failed to save subscriptions: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("UpdateMultipleAsync",
                    $"Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task DeleteAsync(int subscriptionId)
        {
            try
            {
                var subscriptionPrice = await _context.SubscriptionPrices.FindAsync(subscriptionId);
                if (subscriptionPrice == null)
                {
                    await _logService.LogError("DeleteAsync", $"Subscription price not found for ID: {subscriptionId}");
                    throw new Exception("Subscription price not found.");
                }

                if (!string.IsNullOrEmpty(subscriptionPrice.StripePriceId))
                {
                    try
                    {
                        var priceService = new PriceService();
                        await priceService.UpdateAsync(subscriptionPrice.StripePriceId, new PriceUpdateOptions { Active = false });
                        await _logService.LogInfo("DeleteAsync", $"Archived Stripe price ID: {subscriptionPrice.StripePriceId}");
                    }
                    catch (StripeException ex)
                    {
                        await _logService.LogError("DeleteAsync", $"Failed to archive Stripe price ID: {subscriptionPrice.StripePriceId}. Error: {ex.Message}");
                        throw new Exception($"Failed to archive Stripe price: {ex.Message}");
                    }
                }

                _context.SubscriptionPrices.Remove(subscriptionPrice);
                await _context.SaveChangesAsync();
                await _logService.LogInfo("DeleteAsync", $"Deleted subscription price ID: {subscriptionId} from database");
            }
            catch (DbUpdateException ex)
            {
                await _logService.LogError("DeleteAsync", $"Database error deleting subscription ID: {subscriptionId}. Error: {ex.InnerException?.Message ?? ex.Message}");
                throw new Exception("Failed to delete subscription due to database error.", ex);
            }
            catch (Exception ex)
            {
                await _logService.LogError("DeleteAsync", $"Unexpected error deleting subscription ID: {subscriptionId}. Error: {ex.Message}");
                throw;
            }
        }

        private async Task<SubscriptionPrice?> FindExistingSubscriptionAsync(int id, SessionType sessionType, decimal priceGBP, string stripePriceId)
        {
            if (id > 0)
            {
                var subscription = await _context.SubscriptionPrices.FindAsync(id);
                if (subscription != null)
                {
                    return subscription;
                }
            }

            if (!string.IsNullOrEmpty(stripePriceId))
            {
                return await _context.SubscriptionPrices
                    .FirstOrDefaultAsync(p => p.SessionType == sessionType && p.StripePriceId == stripePriceId);
            }

            return await _context.SubscriptionPrices
                .FirstOrDefaultAsync(p => p.SessionType == sessionType && p.PriceGBP == priceGBP);
        }
    }
}