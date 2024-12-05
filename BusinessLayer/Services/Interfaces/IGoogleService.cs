using Google.Apis.Calendar.v3.Data;
using ModelLayer.Models;

public interface IGoogleService {
    Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate);
    Task<bool> CreateEventAdminAsync(Contact contact);
    Task<bool> CreateEventIntervalAsync(Contact contact);
    Task<bool> CreateGoogleCalendarEventAsync(object eventData, string accessToken);
    Task<string> GetAccessTokenAsync(string refreshToken);
}