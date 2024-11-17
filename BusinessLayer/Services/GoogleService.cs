
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class GoogleService : IGoogleService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleService> _logger;
    private CalendarService _calendarService;

    public GoogleService(IHttpContextAccessor httpContextAccessor, HttpClient httpClient, IConfiguration configuration, ILogger<GoogleService> logger) 
    {
        this._httpContextAccessor = httpContextAccessor;
        this._httpClient = httpClient;
        this._configuration = configuration;
        this._logger = logger;
    }

    public async Task InitializeCalendarServiceAsync(string accessToken)
    {
        if (_calendarService != null)
            return;
        
        try
        {
            //var authResult = await _httpContextAccessor.HttpContext.AuthenticateAsync();
            //var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
        
            var initializer = new BaseClientService.Initializer 
            {
                HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
                ApplicationName = "Ítala Veloso"
            };

            _calendarService = new CalendarService(initializer);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error during InitializeCalendarServiceAsync");
        }
    }

    public async Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate)
    {
        string url = $"https://www.googleapis.com/calendar/v3/freeBusy?key={_configuration["GoogleCalendar:ApiKey"]}";

        var requestData = new
        {
            timeMin = startDate.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            timeMax = endDate.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            items = new[]
            {
                new { id = _configuration["GoogleCalendar:CalendarId"] }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestData);
        response.EnsureSuccessStatusCode();

        var freeBusyResponse = await response.Content.ReadFromJsonAsync<FreeBusyResponse>();

        var busyPeriods = freeBusyResponse.Calendars[_configuration["GoogleCalendar:CalendarId"]].Busy;

        return busyPeriods.Select(b => new TimePeriod
        {
            StartDateTimeOffset = DateTime.Parse(b.StartRaw),
            EndDateTimeOffset = DateTime.Parse(b.EndRaw)
        }).ToList();
    }

    public async Task CreateEventAdminAsync(string summary, string description, DateTime start, DateTime end, string timeZone, string userEmail)
    {
        await InitializeCalendarServiceAsync(_configuration["GoogleCalendar:ApiKey"]);

        var newEvent = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = summary,
            Description = description,
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = start,
                TimeZone = timeZone
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = end,
                TimeZone = timeZone
            },
            Attendees = new List<Google.Apis.Calendar.v3.Data.EventAttendee>
            {
                new Google.Apis.Calendar.v3.Data.EventAttendee
                {
                    Email = userEmail
                }
            }
        };

        await _calendarService.Events.Insert(newEvent, "primary").ExecuteAsync();
    }

    public async Task CreateEventUserAsync(string userAccessToken, string summary, string description, DateTime start, DateTime end, string timeZone, string userEmail)
    {
        await InitializeCalendarServiceAsync(userAccessToken);

        var newEvent = new Google.Apis.Calendar.v3.Data.Event
        {
            Summary = summary,
            Description = description,
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = start,
                TimeZone = timeZone
            },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime
            {
                DateTimeDateTimeOffset = end,
                TimeZone = timeZone
            },
            Attendees = new List<Google.Apis.Calendar.v3.Data.EventAttendee>
            {
                new Google.Apis.Calendar.v3.Data.EventAttendee
                {
                    Email = userEmail
                }
            }
        };

        await _calendarService.Events.Insert(newEvent, "primary").ExecuteAsync();
    }
}