using Google.Apis.Calendar.v3.Data;
using ModelLayer.Models;

public interface IGoogleService {
    Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate);
    Task<bool> CreateEventAdminAsync(Contact contact);
}