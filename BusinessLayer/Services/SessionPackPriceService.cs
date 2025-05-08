using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class SessionPackPriceService : ISessionPackPriceService
    {
        private readonly CoachingDbContext _context;
        private readonly ILogService _logService;

        public SessionPackPriceService(CoachingDbContext context, ILogService logService)
        {
            _context = context;
            _logService = logService;
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
            try
            {
                await _logService.LogInfo("SessionPackPriceService.AddOrUpdateAsync", $"Processing session pack price for SessionType: {price.SessionType}, ID: {price.Id}");
                if (price.Id == 0)
                {
                    price.LastUpdated = DateTime.UtcNow;
                    _context.SessionPackPrices.Add(price);
                    await _logService.LogInfo("SessionPackPriceService.AddOrUpdateAsync", $"Added new session pack price for SessionType: {price.SessionType}");
                }
                else
                {
                    var existing = await _context.SessionPackPrices.FindAsync(price.Id);
                    if (existing != null)
                    {
                        existing.Name = price.Name;
                        existing.Description = price.Description;
                        existing.TotalSessions = price.TotalSessions;
                        existing.PriceEUR = price.PriceEUR;
                        existing.SessionType = price.SessionType;
                        existing.LastUpdated = DateTime.UtcNow;
                        _context.SessionPackPrices.Update(existing);
                        await _logService.LogInfo("SessionPackPriceService.AddOrUpdateAsync", $"Updated session pack price for SessionType: {price.SessionType}, ID: {price.Id}");
                    }
                    else
                    {
                        await _logService.LogWarning("SessionPackPriceService.AddOrUpdateAsync", $"Session pack price with ID: {price.Id} not found");
                        throw new InvalidOperationException("Session pack price not found.");
                    }
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackPriceService.AddOrUpdateAsync Error", ex.Message);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                await _logService.LogInfo("SessionPackPriceService.DeleteAsync", $"Deleting session pack price with ID: {id}");
                var price = await _context.SessionPackPrices.FindAsync(id);
                if (price != null)
                {
                    _context.SessionPackPrices.Remove(price);
                    await _context.SaveChangesAsync();
                    await _logService.LogInfo("SessionPackPriceService.DeleteAsync", $"Deleted session pack price with ID: {id}");
                }
                else
                {
                    await _logService.LogWarning("SessionPackPriceService.DeleteAsync", $"No session pack price found with ID: {id}");
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackPriceService.DeleteAsync Error", ex.Message);
                throw;
            }
        }
    }
}