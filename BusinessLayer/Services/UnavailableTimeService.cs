using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using BusinessLayer.Services.Interfaces;
using ModelLayer.Models;

public class UnavailableTimeService : IUnavailableTimeService
{
    private readonly CoachingDbContext _context;

    public UnavailableTimeService(CoachingDbContext context)
    {
        _context = context;
    }

    public async Task<UnavailableTime> CreateUnavailableTimeAsync(UnavailableTime unavailableTime)
    {
        if (unavailableTime == null)
            throw new ArgumentNullException(nameof(unavailableTime));

        _context.UnavailableTimes.Add(unavailableTime);
        await _context.SaveChangesAsync();
        return unavailableTime;
    }

    public async Task<UnavailableTime> GetUnavailableTimeByIdAsync(int id)
    {
        return await _context.UnavailableTimes.FindAsync(id);
    }

    public async Task<IEnumerable<UnavailableTime>> GetAllUnavailableTimesAsync()
    {
        return await _context.UnavailableTimes.ToListAsync();
    }

    public async Task<UnavailableTime> UpdateUnavailableTimeAsync(UnavailableTime unavailableTime)
    {
        if (unavailableTime == null)
            throw new ArgumentNullException(nameof(unavailableTime));

        var existing = await _context.UnavailableTimes.FindAsync(unavailableTime.Id);
        if (existing == null)
            throw new KeyNotFoundException("UnavailableTime not found");

        existing.StartTime = unavailableTime.StartTime;
        existing.EndTime = unavailableTime.EndTime;
        existing.Reason = unavailableTime.Reason;

        _context.UnavailableTimes.Update(existing);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteUnavailableTimeAsync(int id)
    {
        var unavailableTime = await _context.UnavailableTimes.FindAsync(id);
        if (unavailableTime == null)
            return false;

        _context.UnavailableTimes.Remove(unavailableTime);
        await _context.SaveChangesAsync();
        return true;
    }
}