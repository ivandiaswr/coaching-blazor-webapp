using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using BusinessLayer.Services.Interfaces;

public class UnavailableTimeService : IUnavailableTimeService
{
    private readonly CoachingDbContext _context;
    private readonly ILogService _logService;

    public UnavailableTimeService(CoachingDbContext context, ILogService logService)
    {
        _context = context;
        _logService = logService;
    }

    public async Task<UnavailableTime> CreateUnavailableTimeAsync(UnavailableTime unavailableTime)
    {
        if (unavailableTime == null)
            throw new ArgumentNullException(nameof(unavailableTime));

        if (unavailableTime.IsRecurring)
        {
            if (!unavailableTime.DayOfWeek.HasValue)
                throw new ArgumentException("Day of the week is required for recurring unavailability.");
            unavailableTime.Date = null;
        }
        else
        {
            if (!unavailableTime.Date.HasValue)
                throw new ArgumentException("Date is required for one-time unavailability.");
            unavailableTime.DayOfWeek = unavailableTime.Date.Value.DayOfWeek;
        }

        if (!unavailableTime.StartTime.HasValue)
            throw new ArgumentException("Start time is required.");
        if (!unavailableTime.EndTime.HasValue)
            throw new ArgumentException("End time is required.");
        if (unavailableTime.EndTime <= unavailableTime.StartTime)
            throw new ArgumentException("End time must be after start time.");

        try
        {
            _context.UnavailableTimes.Add(unavailableTime);
            await _context.SaveChangesAsync();
            return unavailableTime;
        }
        catch (DbUpdateException ex)
        {
            var errorMessage = $"Database error creating unavailability: {ex.InnerException?.Message ?? ex.Message}";
            await _logService.LogError("CreateUnavailableTimeAsync", errorMessage);
            throw new Exception(errorMessage, ex);
        }
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

        if (unavailableTime.IsRecurring)
        {
            if (!unavailableTime.DayOfWeek.HasValue)
                throw new ArgumentException("Day of the week is required for recurring unavailability.");
            unavailableTime.Date = null;
        }
        else
        {
            if (!unavailableTime.Date.HasValue)
                throw new ArgumentException("Date is required for one-time unavailability.");
            unavailableTime.DayOfWeek = unavailableTime.Date.Value.DayOfWeek;
        }

        if (!unavailableTime.StartTime.HasValue || !unavailableTime.EndTime.HasValue)
            throw new ArgumentException("Start and end times are required.");

        existing.DayOfWeek = unavailableTime.DayOfWeek;
        existing.Date = unavailableTime.Date;
        existing.StartTime = unavailableTime.StartTime;
        existing.EndTime = unavailableTime.EndTime;
        existing.IsRecurring = unavailableTime.IsRecurring;
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