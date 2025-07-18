using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using ModelLayer.Models.Enums;
using Stripe;

namespace BusinessLayer.Services
{
    public class SessionPackPriceService : ISessionPackPriceService
    {
        private readonly CoachingDbContext _context;
        private readonly ILogService _logService;
        private readonly IPaymentService _paymentService;

        public SessionPackPriceService(CoachingDbContext context, ILogService logService, IPaymentService paymentService)
        {
            _context = context;
            _logService = logService;
            _paymentService = paymentService;
        }

        public async Task<List<SessionPackPrice>> GetAllAsync()
        {
            try
            {
                await _logService.LogInfo("SessionPackPriceService.GetAllAsync", "Fetching all session pack prices");
                return await _context.SessionPackPrices.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackPriceService.GetAllAsync Error", ex.Message);
                throw;
            }
        }

        public async Task<List<SessionPackPrice>> GetPricesForSessionTypeAsync(SessionType sessionType)
        {
            try
            {
                await _logService.LogInfo("SessionPackPriceService.GetPricesForSessionTypeAsync", $"Fetching session pack prices for SessionType: {sessionType}");
                return await _context.SessionPackPrices
                    .Where(p => p.SessionType == sessionType)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackPriceService.GetPricesForSessionTypeAsync Error", ex.Message);
                throw;
            }
        }

        public async Task<SessionPackPrice?> GetByIdAsync(int id)
        {
            try
            {
                await _logService.LogInfo("SessionPackPriceService.GetByIdAsync", $"Fetching session pack price with ID: {id}");
                return await _context.SessionPackPrices.FindAsync(id);
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackPriceService.GetByIdAsync Error", ex.Message);
                throw;
            }
        }

        public async Task AddOrUpdateAsync(SessionPackPrice price)
        {
            if (price == null) throw new ArgumentNullException(nameof(price), "Session pack price cannot be null.");

            try
            {
                await _logService.LogInfo("SessionPackPriceService.AddOrUpdateAsync",
                    $"Processing session pack price for SessionType: {price.SessionType}, ID: {price.Id}, Price: {price.PriceGBP}, TotalSessions: {price.TotalSessions}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var existingPackPrice = await _context.SessionPackPrices.FindAsync(price.Id);
                    string stripePriceId = existingPackPrice?.StripePriceId ?? price.StripePriceId;

                    if (price.PriceGBP > 0)
                    {
                        stripePriceId = await _paymentService.CreateOrUpdateSessionPackPriceAsync(
                            price.Name ?? $"Session Pack {price.SessionType}",
                            price.PriceGBP,
                            price.SessionType.ToString(),
                            price.TotalSessions,
                            "GBP");
                    }
                    else
                    {
                        stripePriceId = null;
                    }

                    if (existingPackPrice != null)
                    {
                        existingPackPrice.Name = price.Name ?? string.Empty;
                        existingPackPrice.Description = price.Description ?? string.Empty;
                        existingPackPrice.TotalSessions = price.TotalSessions;
                        existingPackPrice.PriceGBP = price.PriceGBP;
                        existingPackPrice.SessionType = price.SessionType;
                        existingPackPrice.StripePriceId = stripePriceId;
                        existingPackPrice.LastUpdated = DateTime.UtcNow;

                        _context.SessionPackPrices.Update(existingPackPrice);
                        await _logService.LogInfo("AddOrUpdateAsync",
                            $"Updated session pack price with ID: {price.Id}, StripePriceId: {stripePriceId}");
                    }
                    else
                    {
                        price.StripePriceId = stripePriceId;
                        price.LastUpdated = DateTime.UtcNow;
                        _context.SessionPackPrices.Add(price);
                        await _logService.LogInfo("AddOrUpdateAsync",
                            $"Added new session pack price with ID: {price.Id}, StripePriceId: {stripePriceId}");
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    await _logService.LogError("AddOrUpdateAsync",
                        $"Transaction failed for session pack ID: {price.Id}. Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}");
                    throw new Exception($"Failed to save session pack: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("AddOrUpdateAsync",
                    $"Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task UpdateMultipleAsync(List<SessionPackPrice> prices)
        {
            if (prices == null || !prices.Any()) throw new ArgumentNullException(nameof(prices), "Session pack prices cannot be null or empty.");

            try
            {
                await _logService.LogInfo("SessionPackPriceService.UpdateMultipleAsync",
                    $"Processing {prices.Count} session pack prices for SessionType: {prices.FirstOrDefault()?.SessionType}");

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var price in prices)
                    {
                        if (price == null)
                        {
                            await _logService.LogWarning("UpdateMultipleAsync", "Encountered null price object, skipping.");
                            continue;
                        }

                        var existingPackPrice = await _context.SessionPackPrices.FindAsync(price.Id);
                        string stripePriceId = existingPackPrice?.StripePriceId ?? price.StripePriceId;

                        if (price.PriceGBP > 0 && string.IsNullOrEmpty(stripePriceId))
                        {
                            try
                            {
                                stripePriceId = await _paymentService.CreateOrUpdateSessionPackPriceAsync(
                                    price.Name ?? $"Session Pack {price.SessionType}",
                                    price.PriceGBP,
                                    price.SessionType.ToString(),
                                    price.TotalSessions,
                                    "GBP");
                                await _logService.LogInfo("UpdateMultipleAsync",
                                    $"Created/Updated StripePriceId: {stripePriceId} for pack ID: {price.Id}");
                            }
                            catch (Exception ex)
                            {
                                await _logService.LogError("UpdateMultipleAsync",
                                    $"Failed to create/update Stripe price for pack ID: {price.Id}. Error: {ex.Message}");
                                throw;
                            }
                        }
                        else if (price.PriceGBP <= 0)
                        {
                            stripePriceId = null;
                        }

                        if (existingPackPrice != null)
                        {
                            existingPackPrice.Name = price.Name ?? string.Empty;
                            existingPackPrice.Description = price.Description ?? string.Empty;
                            existingPackPrice.TotalSessions = price.TotalSessions > 0 ? price.TotalSessions : 1;
                            existingPackPrice.PriceGBP = price.PriceGBP >= 0 ? price.PriceGBP : 0;
                            existingPackPrice.SessionType = price.SessionType;
                            existingPackPrice.StripePriceId = stripePriceId;
                            existingPackPrice.LastUpdated = DateTime.UtcNow;

                            _context.SessionPackPrices.Update(existingPackPrice);
                            await _logService.LogInfo("UpdateMultipleAsync",
                                $"Updated session pack price with ID: {price.Id}, StripePriceId: {stripePriceId}");
                        }
                        else
                        {
                            price.StripePriceId = stripePriceId;
                            price.LastUpdated = DateTime.UtcNow;
                            _context.SessionPackPrices.Add(price);
                            await _logService.LogInfo("UpdateMultipleAsync",
                                $"Added new session pack price with ID: {price.Id}, StripePriceId: {stripePriceId}");
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    await _logService.LogError("UpdateMultipleAsync",
                        $"Transaction failed for session pack prices. Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}, StackTrace: {ex.StackTrace}");
                    throw new Exception($"Failed to save session packs: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("UpdateMultipleAsync",
                    $"Error: {ex.Message}, Inner: {ex.InnerException?.Message ?? "No inner exception"}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task DeleteAsync(int packId)
        {
            try
            {
                var packPrice = await _context.SessionPackPrices.FindAsync(packId);
                if (packPrice == null)
                {
                    await _logService.LogError("DeleteAsync",
                        $"Session pack price not found for ID: {packId}");
                    throw new Exception("Session pack price not found.");
                }

                var activePacks = await _context.SessionPacks
                    .Where(sp => sp.PriceId == packId &&
                                sp.SessionsRemaining > 0 &&
                                (sp.ExpiresAt == null || sp.ExpiresAt > DateTime.UtcNow))
                    .CountAsync();

                if (activePacks > 0)
                {
                    await _logService.LogWarning("DeleteAsync",
                        $"Cannot delete session pack price ID: {packId} because {activePacks} active session pack(s) are using it.");
                    throw new Exception($"Cannot delete this session pack price. {activePacks} active session pack(s) are currently using it. Please wait for all packs to expire or be consumed before deleting.");
                }

                if (!string.IsNullOrEmpty(packPrice.StripePriceId))
                {
                    try
                    {
                        var priceService = new PriceService();
                        await priceService.UpdateAsync(packPrice.StripePriceId,
                            new PriceUpdateOptions { Active = false });
                        await _logService.LogInfo("DeleteAsync",
                            $"Archived Stripe price ID: {packPrice.StripePriceId}");
                    }
                    catch (StripeException ex)
                    {
                        await _logService.LogError("DeleteAsync",
                            $"Failed to archive Stripe price ID: {packPrice.StripePriceId}. Error: {ex.Message}");
                        throw new Exception($"Failed to archive Stripe price: {ex.Message}");
                    }
                }

                _context.SessionPackPrices.Remove(packPrice);
                await _context.SaveChangesAsync();
                await _logService.LogInfo("DeleteAsync",
                    $"Deleted session pack price ID: {packId} from database");
            }
            catch (DbUpdateException ex)
            {
                await _logService.LogError("DeleteAsync",
                    $"Database error deleting pack ID: {packId}. Error: {ex.InnerException?.Message ?? ex.Message}");
                throw new Exception("Failed to delete session pack due to database error.", ex);
            }
            catch (Exception ex)
            {
                await _logService.LogError("DeleteAsync",
                    $"Unexpected error deleting pack ID: {packId}. Error: {ex.Message}");
                throw;
            }
        }
    }
}