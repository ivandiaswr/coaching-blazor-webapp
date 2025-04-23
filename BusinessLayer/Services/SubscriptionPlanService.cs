using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly CoachingDbContext _context;

        public SubscriptionPlanService(CoachingDbContext context)
        {
            _context = context;
        }

        public async Task<List<SubscriptionPlan>> GetAllAsync() =>
            await _context.SubscriptionPlans.ToListAsync();

        public async Task<SubscriptionPlan?> GetByIdAsync(int id) =>
            await _context.SubscriptionPlans.FindAsync(id);

        public async Task AddOrUpdateAsync(SubscriptionPlan plan)
        {
            if (plan.Id == 0)
                _context.SubscriptionPlans.Add(plan);
            else
                _context.SubscriptionPlans.Update(plan);

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan != null)
            {
                _context.SubscriptionPlans.Remove(plan);
                await _context.SaveChangesAsync();
            }
        }
    }
}