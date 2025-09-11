using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModelLayer.Models;

namespace BusinessLayer.Services
{
    public class CurrencyConversionService : ICurrencyConversionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogService _logService;
        private readonly IConfiguration _configuration;
        private readonly CoachingDbContext _context;
        private Dictionary<string, decimal> _exchangeRates = new();
        private DateTime _lastUpdated = DateTime.MinValue;
        private readonly Dictionary<string, decimal> _fallbackRates = new()
        {
            { "EUR", 1.18m },
            { "USD", 1.25m },
        };

        // Cache settings
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(12); // Refresh every 12 hours
        private readonly TimeSpan _apiCooldown = TimeSpan.FromMinutes(30); // Don't call API more than once every 30 minutes
        private DateTime _lastApiCall = DateTime.MinValue;

        public CurrencyConversionService(HttpClient httpClient, ILogService logService, IConfiguration configuration, CoachingDbContext context)
        {
            _httpClient = httpClient;
            _logService = logService;
            _configuration = configuration;
            _context = context;
        }

        public async Task<(decimal Price, string Error)> ConvertPrice(decimal priceGBP, string targetCurrency)
        {
            if (string.IsNullOrEmpty(targetCurrency))
            {
                await _logService.LogWarning("ConvertPrice", "Target currency is null or empty. Falling back to GBP.");
                return (priceGBP, string.Empty);
            }

            if (string.Equals(targetCurrency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                return (priceGBP, string.Empty);
            }

            try
            {
                // Try to get rate from database cache first
                var cachedRate = await GetCachedExchangeRate("GBP", targetCurrency);
                if (cachedRate != null)
                {
                    var convertedPrice = CalculateConvertedPrice(priceGBP, cachedRate.Rate, targetCurrency);
                    await _logService.LogInfo("ConvertPrice", $"Converted {priceGBP} GBP to {convertedPrice} {targetCurrency} using cached rate {cachedRate.Rate}.");
                    return (convertedPrice, string.Empty);
                }

                // If not cached or expired, try to refresh from API
                await RefreshExchangeRatesIfNeeded();

                // Try again from database after potential API refresh
                cachedRate = await GetCachedExchangeRate("GBP", targetCurrency);
                if (cachedRate != null)
                {
                    var convertedPrice = CalculateConvertedPrice(priceGBP, cachedRate.Rate, targetCurrency);
                    await _logService.LogInfo("ConvertPrice", $"Converted {priceGBP} GBP to {convertedPrice} {targetCurrency} using refreshed rate {cachedRate.Rate}.");
                    return (convertedPrice, string.Empty);
                }

                // Fall back to hardcoded rates
                if (_fallbackRates.TryGetValue(targetCurrency, out var fallbackRate))
                {
                    var convertedPrice = CalculateConvertedPrice(priceGBP, fallbackRate, targetCurrency);
                    await _logService.LogWarning("ConvertPrice", $"No cached or API rate for {targetCurrency}. Used fallback rate {fallbackRate}. Converted {priceGBP} GBP to {convertedPrice} {targetCurrency}.");

                    // Cache the fallback rate for future use
                    await CacheExchangeRate("GBP", targetCurrency, fallbackRate, "Fallback");

                    return (convertedPrice, string.Empty);
                }

                await _logService.LogError("ConvertPrice", $"No exchange rate available for {targetCurrency}. Returning {priceGBP} GBP.");
                return (priceGBP, $"Unable to convert to {targetCurrency}: no exchange rate available.");
            }
            catch (Exception ex)
            {
                await _logService.LogError("ConvertPrice", $"Error during currency conversion: {ex.Message}");
                return (priceGBP, $"Currency conversion error: {ex.Message}");
            }
        }

        private decimal CalculateConvertedPrice(decimal priceGBP, decimal rate, string targetCurrency)
        {
            var isZeroDecimal = new[] { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" }
                .Contains(targetCurrency, StringComparer.OrdinalIgnoreCase);

            return isZeroDecimal ? Math.Round(priceGBP * rate) : Math.Round(priceGBP * rate, 2);
        }

        private async Task<ExchangeRate?> GetCachedExchangeRate(string fromCurrency, string toCurrency)
        {
            try
            {
                var now = DateTime.UtcNow;
                var cachedRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(er => er.FromCurrency == fromCurrency.ToUpper()
                                             && er.ToCurrency == toCurrency.ToUpper()
                                             && er.ExpiresAt > now);

                if (cachedRate != null)
                {
                    await _logService.LogInfo("GetCachedExchangeRate", $"Found cached rate for {fromCurrency} to {toCurrency}: {cachedRate.Rate} (expires: {cachedRate.ExpiresAt})");
                }

                return cachedRate;
            }
            catch (Exception ex)
            {
                await _logService.LogError("GetCachedExchangeRate", $"Error retrieving cached rate: {ex.Message}");
                return null;
            }
        }

        private async Task CacheExchangeRate(string fromCurrency, string toCurrency, decimal rate, string source)
        {
            try
            {
                var now = DateTime.UtcNow;
                var existingRate = await _context.ExchangeRates
                    .FirstOrDefaultAsync(er => er.FromCurrency == fromCurrency.ToUpper()
                                             && er.ToCurrency == toCurrency.ToUpper());

                if (existingRate != null)
                {
                    // Update existing rate
                    existingRate.Rate = rate;
                    existingRate.LastUpdated = now;
                    existingRate.ExpiresAt = now.Add(_cacheExpiration);
                    existingRate.Source = source;
                    _context.ExchangeRates.Update(existingRate);
                }
                else
                {
                    // Create new rate
                    var newRate = new ExchangeRate
                    {
                        FromCurrency = fromCurrency.ToUpper(),
                        ToCurrency = toCurrency.ToUpper(),
                        Rate = rate,
                        LastUpdated = now,
                        ExpiresAt = now.Add(_cacheExpiration),
                        Source = source
                    };
                    await _context.ExchangeRates.AddAsync(newRate);
                }

                await _context.SaveChangesAsync();
                await _logService.LogInfo("CacheExchangeRate", $"Cached rate {fromCurrency} to {toCurrency}: {rate} (source: {source})");
            }
            catch (Exception ex)
            {
                await _logService.LogError("CacheExchangeRate", $"Error caching rate: {ex.Message}");
            }
        }

        private async Task RefreshExchangeRatesIfNeeded()
        {
            try
            {
                var now = DateTime.UtcNow;

                // Check if we recently called the API
                if (now < _lastApiCall.Add(_apiCooldown))
                {
                    await _logService.LogInfo("RefreshExchangeRatesIfNeeded", $"API cooldown active. Last call: {_lastApiCall}, Next allowed: {_lastApiCall.Add(_apiCooldown)}");
                    return;
                }

                // Check if we have any expired rates that need refreshing
                var hasExpiredRates = await _context.ExchangeRates
                    .AnyAsync(er => er.ExpiresAt <= now);

                if (!hasExpiredRates)
                {
                    await _logService.LogInfo("RefreshExchangeRatesIfNeeded", "No expired rates found. Skipping API call.");
                    return;
                }

                await _logService.LogInfo("RefreshExchangeRatesIfNeeded", "Found expired rates. Calling API to refresh.");
                await FetchExchangeRatesFromAPI();
                _lastApiCall = now;
            }
            catch (Exception ex)
            {
                await _logService.LogError("RefreshExchangeRatesIfNeeded", $"Error checking/refreshing rates: {ex.Message}");
            }
        }
        private async Task FetchExchangeRatesFromAPI()
        {
            try
            {
                var apiKey = _configuration["ExchangeRateApi:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    await _logService.LogError("FetchExchangeRatesFromAPI", "ExchangeRateApi:ApiKey is missing in configuration.");
                    return;
                }

                await _logService.LogInfo("FetchExchangeRatesFromAPI", $"Fetching exchange rates with API key ending in {apiKey[^4..]}");
                var response = await _httpClient.GetAsync($"https://v6.exchangerate-api.com/v6/{apiKey}/latest/GBP");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    await _logService.LogError("FetchExchangeRatesFromAPI", $"HTTP error: {response.StatusCode}, Reason: {errorContent}");
                    return;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                await _logService.LogInfo("FetchExchangeRatesFromAPI", $"Raw API response: {jsonResponse}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new DecimalConverter() }
                };
                var exchangeResponse = JsonSerializer.Deserialize<ExchangeRateResponse>(jsonResponse, options);

                if (exchangeResponse?.ConversionRates != null && exchangeResponse.ConversionRates.Any())
                {
                    await _logService.LogInfo("FetchExchangeRatesFromAPI", $"Successfully fetched {exchangeResponse.ConversionRates.Count} exchange rates from API.");

                    // Cache all rates in database
                    foreach (var rate in exchangeResponse.ConversionRates)
                    {
                        await CacheExchangeRate("GBP", rate.Key, rate.Value, "API");
                    }

                    await _logService.LogInfo("FetchExchangeRatesFromAPI", "All exchange rates cached in database successfully.");
                }
                else
                {
                    await _logService.LogError("FetchExchangeRatesFromAPI", "Invalid or empty exchange rate response.");
                }
            }
            catch (JsonException ex)
            {
                await _logService.LogError("FetchExchangeRatesFromAPI", $"JSON deserialization failed: {ex.Message}, Line: {ex.LineNumber}, Path: {ex.Path}");
            }
            catch (HttpRequestException ex)
            {
                await _logService.LogError("FetchExchangeRatesFromAPI", $"HTTP request failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _logService.LogError("FetchExchangeRatesFromAPI", $"Unexpected error: {ex.Message}");
            }
        }

        private async Task FetchExchangeRates()
        {
            // Legacy method - redirect to new method
            await FetchExchangeRatesFromAPI();
        }

        Task ICurrencyConversionService.FetchExchangeRates()
        {
            return FetchExchangeRates();
        }
    }

    public class ExchangeRateResponse
    {
        [JsonPropertyName("conversion_rates")]
        public Dictionary<string, decimal> ConversionRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class DecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetDecimal(out var value))
                {
                    return value;
                }
                if (reader.TryGetDouble(out var doubleValue))
                {
                    return (decimal)doubleValue;
                }
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                if (decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
            }
            throw new JsonException($"Unable to convert JSON token {reader.TokenType} to decimal.");
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}