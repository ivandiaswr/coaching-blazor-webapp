
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Google.Apis.Calendar.v3.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelLayer.Models;

public class GoogleService : IGoogleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly IHelperService _helperService;
    private readonly ILogger<GoogleService> _logger;
    private readonly IUserRefreshTokenService _userRefreshTokenService;

    public GoogleService(IHttpClientFactory httpClientFactory, 
        HttpClient httpClient, 
        IHelperService configuration, 
        ILogger<GoogleService> logger, 
        IUserRefreshTokenService userRefreshTokenService) 
    {
        this._httpClientFactory = httpClientFactory;
        this._httpClient = httpClient;
        this._helperService = configuration;
        this._logger = logger;
        this._userRefreshTokenService = userRefreshTokenService;
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

    public async Task<bool> CreateGoogleCalendarEventAsync(object eventData, string accessToken)
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
            _logger.LogError($"Failed to create event: {response.StatusCode}, {errorContent}");
            throw new Exception($"Failed to create event: {response.StatusCode}, {errorContent}");
        }

        _logger.LogInformation("Event created successfully.");
        return true;
    }

    public async Task<bool> CreateEventAdminAsync(Contact contact)
    {
        var refreshToken = await _userRefreshTokenService.GetRefreshTokenByLatest();

        var accessToken = await GetAccessTokenAsync(refreshToken);

        var eventData = new
        {
            summary = $"Meeting with {contact.FullName}",
            description = contact.Message,
            start = new { dateTime = contact.PreferredDateTime.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end = new { dateTime = contact.PreferredDateTime.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            attendees = new[] { new { email = contact?.Email } },
            conferenceData = new
            {
                createRequest = new
                {
                    requestId = Guid.NewGuid().ToString(),
                    conferenceSolutionKey = new { type = "hangoutsMeet" }
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
            start = new { dateTime = contact.PreferredDateTime.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end = new { dateTime = contact.PreferredDateTime.AddMinutes(45).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" }
        };

        return await CreateGoogleCalendarEventAsync(eventData, accessToken);
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
            _logger.LogError($"Failed to refresh access token: {response.StatusCode}, {errorContent}");
            throw new Exception($"Failed to refresh access token: {response.StatusCode}, {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(content);

        return tokenResponse?.AccessToken ?? throw new Exception("Failed to obtain access token.");
    }


}