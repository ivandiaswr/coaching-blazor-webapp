using System.Net.Http.Json;
using BusinessLayer.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer.Services
{
    public class CurrencyConversionService : ICurrencyConversionService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogService _logService;
        private readonly IConfiguration _configuration;
        private Dictionary<string, decimal> _exchangeRates = new();
        private DateTime _lastUpdated = DateTime.MinValue;
        private readonly Dictionary<string, decimal> _fallbackRates = new()
        {
            { "EUR", 1.18m },
            { "USD", 1.25m },
        };

        public CurrencyConversionService(HttpClient httpClient, ILogService logService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logService = logService;
            _configuration = configuration;
        }

        public async Task<decimal> ConvertPrice(decimal priceGBP, string targetCurrency)
        {
            if (string.IsNullOrEmpty(targetCurrency))
            {
                await _logService.LogWarning("ConvertPrice", "Target currency is null or empty. Falling back to GBP.");
                return priceGBP;
            }

            if (string.Equals(targetCurrency, "GBP", StringComparison.OrdinalIgnoreCase))
            {
                return priceGBP;
            }

            if (DateTime.UtcNow > _lastUpdated.AddHours(24) || !_exchangeRates.ContainsKey(targetCurrency.ToUpper()))
            {
                await FetchExchangeRates();
            }

            if (_exchangeRates.TryGetValue(targetCurrency.ToUpper(), out var rate))
            {
                var isZeroDecimal = new[] { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" }
                    .Contains(targetCurrency.ToUpper());
                var convertedPrice = isZeroDecimal ? Math.Round(priceGBP * rate) : Math.Round(priceGBP * rate, 2);
                await _logService.LogInfo("ConvertPrice", $"Converted {priceGBP} GBP to {convertedPrice} {targetCurrency} using rate {rate}.");
                return convertedPrice;
            }

            if (_fallbackRates.TryGetValue(targetCurrency.ToUpper(), out var fallbackRate))
            {
                var isZeroDecimal = new[] { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" }
                    .Contains(targetCurrency.ToUpper());
                var convertedPrice = isZeroDecimal ? Math.Round(priceGBP * fallbackRate) : Math.Round(priceGBP * fallbackRate, 2);
                await _logService.LogWarning("ConvertPrice", $"No API exchange rate for {targetCurrency}. Used fallback rate {fallbackRate}. Converted {priceGBP} GBP to {convertedPrice} {targetCurrency}.");
                return convertedPrice;
            }

            await _logService.LogError("ConvertPrice", $"No exchange rate or fallback for {targetCurrency}. Returning {priceGBP} GBP.");
            return priceGBP;
        }

        private async Task FetchExchangeRates()
        {
            try
            {
                var apiKey = _configuration["ExchangeRateApi:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    await _logService.LogError("FetchExchangeRates", "ExchangeRateApi:ApiKey is missing in configuration.");
                    _exchangeRates = new Dictionary<string, decimal>();
                    return;
                }

                var response = await _httpClient.GetAsync($"https://v6.exchangerate-api.com/v6/{apiKey}/latest/GBP");
                if (!response.IsSuccessStatusCode)
                {
                    await _logService.LogError("FetchExchangeRates", $"HTTP error: {response.StatusCode}, Reason: {await response.Content.ReadAsStringAsync()}");
                    _exchangeRates = new Dictionary<string, decimal>();
                    return;
                }

                var exchangeResponse = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>();
                if (exchangeResponse?.ConversionRates != null && exchangeResponse.ConversionRates.Any())
                {
                    _exchangeRates = exchangeResponse.ConversionRates;
                    _lastUpdated = DateTime.UtcNow;
                    await _logService.LogInfo("FetchExchangeRates", $"Exchange rates updated successfully. Rates: {string.Join(", ", _exchangeRates.Select(r => $"{r.Key}: {r.Value}"))}");
                }
                else
                {
                    await _logService.LogError("FetchExchangeRates", "Invalid or empty exchange rate response.");
                    _exchangeRates = new Dictionary<string, decimal>();
                }
            }
            catch (HttpRequestException ex)
            {
                await _logService.LogError("FetchExchangeRates", $"HTTP request failed: {ex.Message}, StatusCode: {ex.StatusCode}, InnerException: {ex.InnerException?.Message}");
                _exchangeRates = new Dictionary<string, decimal>();
            }
            catch (Exception ex)
            {
                await _logService.LogError("FetchExchangeRates", $"Unexpected error: {ex.Message}, StackTrace: {ex.StackTrace}");
                _exchangeRates = new Dictionary<string, decimal>();
            }
        }

        Task ICurrencyConversionService.FetchExchangeRates()
        {
            return FetchExchangeRates();
        }
    }
}

public class ExchangeRateResponse
{
    public Dictionary<string, decimal> ConversionRates { get; set; }
}