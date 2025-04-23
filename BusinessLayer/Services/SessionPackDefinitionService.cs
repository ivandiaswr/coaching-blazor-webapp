using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class SessionPackDefinitionService : ISessionPackDefinitionService
    {
        private readonly CoachingDbContext _context;

        public SessionPackDefinitionService(CoachingDbContext context)
        {
            _context = context;
        }

        public async Task<List<SessionPackDefinition>> GetAllAsync()
        {
            return await _context.SessionPackDefinitions.ToListAsync();
        }

        public async Task CreateAsync(SessionPackDefinition definition)
        {
            _context.SessionPackDefinitions.Add(definition);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SessionPackDefinition definition)
        {
            _context.SessionPackDefinitions.Update(definition);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var pack = await _context.SessionPackDefinitions.FindAsync(id);
            if (pack != null)
            {
                _context.SessionPackDefinitions.Remove(pack);
                await _context.SaveChangesAsync();
            }
        }
    }
}