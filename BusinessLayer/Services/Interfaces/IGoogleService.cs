using Google.Apis.Calendar.v3.Data;

public interface IGoogleService {
    Task InitializeCalendarServiceAsync(string accessToken);
    Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate);
    Task CreateEventAdminAsync(string summary, string description, DateTime start, DateTime end, string timeZone, string userEmail);
    Task CreateEventUserAsync(string userAccessToken, string summary, string description, DateTime start, DateTime end, string timeZone, string userEmail);
}