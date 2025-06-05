using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using Stripe;

namespace BusinessLayer.Services
{
    public class SubscriptionPriceService : ISubscriptionPriceService
    {
        private readonly CoachingDbContext _context;
        private readonly IPaymentService _paymentService;
        private readonly ILogService _logService;

        public SubscriptionPriceService(CoachingDbContext context, ILogService logService, IPaymentService paymentService)
        {
            _context = context;
            _logService = logService;
            _paymentService = paymentService;
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

        public async Task AddOrUpdateAsync(SubscriptionPrice price)
        {
            try
            {
                await _logService.LogInfo("SubscriptionPriceService.AddOrUpdateAsync", $"Processing subscription price for SessionType: {price.SessionType}, ID: {price.Id}");
                string stripePriceId = price.StripePriceId;

                // Create or update Stripe Price if PriceGBP is set
                if (price.PriceGBP > 0)
                {
                    stripePriceId = await _paymentService.CreateOrUpdateSubscriptionPriceAsync(
                        price.Name,
                        price.PriceGBP,
                        price.SessionType.ToString());
                }

                if (price.Id == 0)
                {
                    price.StripePriceId = stripePriceId;
                    price.LastUpdated = DateTime.UtcNow;
                    _context.SubscriptionPrices.Add(price);
                    await _logService.LogInfo("SubscriptionPriceService.AddOrUpdateAsync", $"Added new subscription price for SessionType: {price.SessionType}, StripePriceId: {stripePriceId}");
                }
                else
                {
                    var existing = await _context.SubscriptionPrices.FindAsync(price.Id);
                    if (existing != null)
                    {
                        existing.Name = price.Name;
                        existing.Description = price.Description;
                        existing.MonthlyLimit = price.MonthlyLimit;
                        existing.PriceGBP = price.PriceGBP;
                        existing.SessionType = price.SessionType;
                        existing.StripePriceId = stripePriceId;
                        existing.LastUpdated = DateTime.UtcNow;
                        _context.SubscriptionPrices.Update(existing);
                        await _logService.LogInfo("SubscriptionPriceService.AddOrUpdateAsync", $"Updated subscription price for SessionType: {price.SessionType}, ID: {price.Id}, StripePriceId: {stripePriceId}");
                    }
                    else
                    {
                        await _logService.LogWarning("SubscriptionPriceService.AddOrUpdateAsync", $"Subscription price with ID: {price.Id} not found");
                        throw new InvalidOperationException("Subscription price not found.");
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SubscriptionPriceService.AddOrUpdateAsync Error", ex.Message);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                await _logService.LogInfo("SubscriptionPriceService.DeleteAsync", $"Deleting subscription price with ID: {id}");
                var price = await _context.SubscriptionPrices.FindAsync(id);
                if (price != null)
                {
                    // Archive the Stripe Price if it exists
                    if (!string.IsNullOrEmpty(price.StripePriceId))
                    {
                        try
                        {
                            var priceService = new PriceService();
                            var updateOptions = new PriceUpdateOptions { Active = false };
                            await priceService.UpdateAsync(price.StripePriceId, updateOptions);
                            await _logService.LogInfo("SubscriptionPriceService.DeleteAsync", $"Archived Stripe Price ID: {price.StripePriceId}");
                        }
                        catch (StripeException ex)
                        {
                            await _logService.LogWarning("SubscriptionPriceService.DeleteAsync", $"Failed to archive Stripe Price ID: {price.StripePriceId}, Error: {ex.Message}");
                        }
                    }

                    _context.SubscriptionPrices.Remove(price);
                    await _context.SaveChangesAsync();
                    await _logService.LogInfo("SubscriptionPriceService.DeleteAsync", $"Deleted subscription price with ID: {id}");
                }
                else
                {
                    await _logService.LogWarning("SubscriptionPriceService.DeleteAsync", $"No subscription price found with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("SubscriptionPriceService.DeleteAsync Error", ex.Message);
                throw;
            }
        }
    }
}