using Google.Apis.Calendar.v3.Data;
using ModelLayer.Models;

public interface IGoogleService {
    Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate);
    Task<string> CreateEventAdminAsync(Contact contact);
    Task<bool> CreateEventIntervalAsync(Contact contact);
    Task<string> CreateGoogleCalendarEventAsync(object eventData, string accessToken);
    Task<string> GetAccessTokenAsync(string refreshToken);
}