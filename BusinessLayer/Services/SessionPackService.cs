using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using ModelLayer.Models.Enums;

namespace BusinessLayer.Services
{
    public class SessionPackService : ISessionPackService
    {
        private readonly CoachingDbContext _context;
        private readonly ILogService _logService;

        public SessionPackService(CoachingDbContext context, ILogService logService)
        {
            _logService = logService;
            _context = context;
        }

        public async Task<List<SessionPack>> GetAllAsync() =>
            await _context.SessionPacks.ToListAsync();

        public async Task CreateAsync(SessionPack pack)
        {
            pack.PurchasedAt = DateTime.UtcNow;
            _context.SessionPacks.Add(pack);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SessionPack pack)
        {
            _context.SessionPacks.Update(pack);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var pack = await _context.SessionPacks.FindAsync(id);
            if (pack != null)
            {
                _context.SessionPacks.Remove(pack);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ConsumeSession(string userId, string packId)
        {
            try
            {
                var pack = await _context.SessionPacks
                    .Include(p => p.Price)
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Id.ToString() == packId && p.SessionsRemaining > 0 && 
                                            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow));
                
                if (pack == null)
                {
                    await _logService.LogError("ConsumeSession", $"No valid pack found for UserId: {userId}, PackId: {packId}");
                    return false;
                }

                pack.SessionsRemaining--;
                await _context.SaveChangesAsync();
                await _logService.LogInfo("ConsumeSession", $"Consumed 1 session from PackId: {packId}, UserId: {userId}, Remaining: {pack.SessionsRemaining}");
                return true;
            }
            catch (Exception ex)
            {
                await _logService.LogError("ConsumeSession", ex.Message);
                return false;
            }
        }
        
        public async Task<bool> RollbackSessionConsumption(string userId, string packId)
        {
            try
            {
                var pack = await _context.SessionPacks
                    .FirstOrDefaultAsync(p => p.Id.ToString() == packId && p.UserId == userId);

                if (pack == null)
                {
                    await _logService.LogError("RollbackSessionConsumption", $"Session pack not found for PackId: {packId}, UserId: {userId}");
                    return false;
                }

                pack.SessionsRemaining += 1;
                await UpdateAsync(pack);
                await _logService.LogInfo("RollbackSessionConsumption", $"Restored 1 session for PackId: {packId}, UserId: {userId}, New Remaining: {pack.SessionsRemaining}");
                return true;
            }
            catch (Exception ex)
            {
                await _logService.LogError("RollbackSessionConsumption Error", $"PackId: {packId}, UserId: {userId}, Error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetRemainingSessions(string userId)
        {
            var pack = await _context.SessionPacks
                .Where(p => p.UserId == userId && p.SessionsRemaining > 0 &&
                            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
                .FirstOrDefaultAsync();

            return pack?.SessionsRemaining ?? 0;
        }

        public async Task<List<SessionPack>> GetUserPacksAsync(string userId)
        {
            return await _context.SessionPacks
                .Include(p => p.Price)
                .Where(p => p.UserId == userId && p.SessionsRemaining > 0)
                .ToListAsync();
        }

        public async Task RestoreSession(string userId)
        {
            var userPack = await _context.SessionPacks
                .Include(p => p.Price)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.PurchasedAt)
                .FirstOrDefaultAsync();

            if (userPack != null)
            {
                userPack.SessionsRemaining++;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<SessionPackPrice>> GetPricesForSessionTypeAsync(SessionType sessionType)
        {
            try
            {
                var prices = await _context.SessionPackPrices
                    .Where(p => p.SessionType == sessionType)
                    .ToListAsync();

                await _logService.LogInfo("SessionPackService.GetPricesForSessionTypeAsync", $"Retrieved {prices.Count} prices for SessionType: {sessionType}");
                return prices;
            }
            catch (Exception ex)
            {
                await _logService.LogError("SessionPackService.GetPricesForSessionTypeAsync Error", ex.Message);
                return new List<SessionPackPrice>();
            }
        }

    }
}