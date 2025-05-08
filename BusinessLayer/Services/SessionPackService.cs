using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class SessionPackService : ISessionPackService
    {
        private readonly CoachingDbContext _context;

        public SessionPackService(CoachingDbContext context)
        {
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

        public async Task<bool> ConsumeSession(string userId, int? packId)
        {
            SessionPack? pack;
            if (packId.HasValue)
            {
                pack = await _context.SessionPacks
                    .Where(p => p.Id == packId && p.UserId == userId && p.SessionsRemaining > 0 &&
                                (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
                    .FirstOrDefaultAsync();
            }
            else
            {
                pack = await _context.SessionPacks
                    .Where(p => p.UserId == userId && p.SessionsRemaining > 0 &&
                                (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
                    .OrderBy(p => p.PurchasedAt)
                    .FirstOrDefaultAsync();
            }

            if (pack == null || pack.Price == null)
                return false;

            pack.SessionsRemaining--;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> GetRemainingSessions(string userId)
        {
            var pack = await _context.SessionPacks
                .Where(p => p.UserId == userId && p.SessionsRemaining > 0 && 
                            (p.ExpiresAt == null || p.ExpiresAt > DateTime.UtcNow))
                .OrderBy(p => p.PurchasedAt)
                .FirstOrDefaultAsync();

            return pack?.SessionsRemaining ?? 0;
        }

        public async Task<List<SessionPack>> GetUserPacksAsync(string userId)
        {
            return await _context.SessionPacks
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
    }
}