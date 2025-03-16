
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BusinessLayer.Services.Interfaces;
using Google.Apis.Calendar.v3.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelLayer.Models;

public class GoogleService : IGoogleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly IHelperService _helperService;
    private readonly IUserRefreshTokenService _userRefreshTokenService;
    private readonly ILogService _logService;

    public GoogleService(IHttpClientFactory httpClientFactory, 
        HttpClient httpClient, 
        IHelperService configuration,  
        IUserRefreshTokenService userRefreshTokenService,
        ILogService logService) 
    {
        this._httpClientFactory = httpClientFactory;
        this._httpClient = httpClient;
        this._helperService = configuration;
        this._userRefreshTokenService = userRefreshTokenService;
        this._logService = logService;
    }

    public async Task<List<TimePeriod>> GetBusyTimes(DateTime startDate, DateTime endDate)
    {
        string url = $"https://www.googleapis.com/calendar/v3/freeBusy?key={_helperService.GetConfigValue("GoogleCalendar:ApiKey")}";

        var requestData = new
        {
            timeMin = startDate.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            timeMax = endDate.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            items = new[]
            {
                new { id = _helperService.GetConfigValue("GoogleCalendar:CalendarId")}
            }
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestData);
        response.EnsureSuccessStatusCode();

        var freeBusyResponse = await response.Content.ReadFromJsonAsync<FreeBusyResponse>();

        var busyPeriods = freeBusyResponse.Calendars[_helperService.GetConfigValue("GoogleCalendar:CalendarId")].Busy;

        return busyPeriods.Select(b => new TimePeriod
        {
            StartDateTimeOffset = DateTime.Parse(b.StartRaw),
            EndDateTimeOffset = DateTime.Parse(b.EndRaw)
        }).ToList();
    }

    public async Task<string> CreateGoogleCalendarEventAsync(object eventData, string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/primary/events?conferenceDataVersion=1&sendUpdates=all")
        {
            Content = new StringContent(JsonSerializer.Serialize(eventData), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            await _logService.LogError("Error during CreateGoogleCalendarEventAsync", $"Failed to create event: {response.StatusCode}, {errorContent}");
            throw new Exception($"Failed to create event: {response.StatusCode}, {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();

        var calendarEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<Event>(content);

        return calendarEvent?.ConferenceData?.EntryPoints?.FirstOrDefault(ep => ep.EntryPointType == "video")?.Uri ?? string.Empty;
    }

    public async Task<string> CreateEventAdminAsync(Contact contact)
    {
        var refreshToken = await _userRefreshTokenService.GetRefreshTokenByLatest();

        var accessToken = await GetAccessTokenAsync(refreshToken);

        var eventData = new
        {
            summary = $"Meeting with {contact.FullName}",
            description = contact.Message,
            start = new
            {
                dateTime = contact.PreferredDateTime.ToString("yyyy-MM-ddTHH:mm:ss"), 
                timeZone = "UTC"
            },
            end = new 
            {
                dateTime = contact.PreferredDateTime.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"), 
                timeZone = "UTC" 
            },
            attendees = new[] 
            { 
                new { email = contact?.Email } 
            },
            conferenceData = new
            {
                createRequest = new
                {
                    requestId = Guid.NewGuid().ToString(),
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
                }
            },
            Reminders = new 
            {
                UseDefault = false,
                Overrides = new List<EventReminder>
                {
                    new EventReminder
                    {
                        Method = "popup",
                        Minutes = 10
                    },
                    new EventReminder
                    {
                        Method = "email",
                        Minutes = 30
                    }
                }
            }
        };

        return await CreateGoogleCalendarEventAsync(eventData, accessToken);
    }

   public async Task<bool> CreateEventIntervalAsync(Contact contact)
    {
        var refreshToken = await _userRefreshTokenService.GetRefreshTokenByLatest();

        var accessToken = await GetAccessTokenAsync(refreshToken);

        var eventData = new
        {
            summary = $"Meeting Interval after {contact.FullName}",
            description = "Interval",
            start = new 
            { 
                dateTime = contact.PreferredDateTime.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"), 
                timeZone = "UTC" 
            },
            end = new 
            { 
                dateTime = contact.PreferredDateTime.AddMinutes(45).ToString("yyyy-MM-ddTHH:mm:ss"), 
                timeZone = "UTC" 
            }
        };

        var googleMeetLink = await CreateGoogleCalendarEventAsync(eventData, accessToken);

        return true;
    }

    public async Task<string> GetAccessTokenAsync(string refreshToken)
    {
        var clientId = _helperService.GetConfigValue("GoogleCalendar:ClientId");
        var clientSecret = _helperService.GetConfigValue("GoogleCalendar:ClientSecret");

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            })
        };

        var client = _httpClientFactory.CreateClient();
        var response = await client.SendAsync(tokenRequest);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            await _logService.LogError("Error during GetAccessTokenAsync", $"Failed to create event: {response.StatusCode}, {errorContent}");

            // await _emailSubscriptionService.SendCustomEmailAsync(new List<string> { _helperService.GetConfigValue("AdminEmail:Primary"), _helperService.GetConfigValue("AdminEmail:Secondary")},
            //                                                         "Schedule Error", 
            //                                                         @$"StatusCode: {response.StatusCode}
            //                                                         <br>ErrorContent: {errorContent}
            //                                                         <br>Response: {response}");

            throw new Exception($"Failed to refresh access token: {response.StatusCode}, {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(content);

        return tokenResponse?.AccessToken ?? throw new Exception("Failed to obtain access token.");
    }
}