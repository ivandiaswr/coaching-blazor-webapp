using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class SessionPriceService : ISessionPriceService
    {
        private readonly CoachingDbContext _context;

        public SessionPriceService(CoachingDbContext context)
        {
            _context = context;
        }

        public async Task<List<SessionPrice>> GetAllAsync()
            => await _context.SessionPrices.ToListAsync();

        public async Task<SessionPrice?> GetBySessionTypeAsync(SessionType type)
            => await _context.SessionPrices.FirstOrDefaultAsync(p => p.SessionType == type);

        public async Task AddOrUpdateAsync(SessionPrice price)
        {
            var existing = await GetBySessionTypeAsync(price.SessionType);
            if (existing != null)
            {
                existing.PriceEUR = price.PriceEUR;
                existing.LastUpdated = DateTime.UtcNow;
            }
            else
            {
                price.LastUpdated = DateTime.UtcNow;
                _context.SessionPrices.Add(price);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var toDelete = await _context.SessionPrices.FindAsync(id);
            if (toDelete != null)
            {
                _context.SessionPrices.Remove(toDelete);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<SessionPrice?> GetPriceForSessionTypeAsync(SessionType sessionType)
        {
            return await _context.SessionPrices
                .Where(sp => sp.SessionType == sessionType)
                .OrderByDescending(sp => sp.LastUpdated)
                .FirstOrDefaultAsync();
        }
    }
}