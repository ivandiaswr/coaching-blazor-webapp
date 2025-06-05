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

        public CurrencyConversionService(HttpClient httpClient, ILogService logService, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logService = logService;
            _configuration = configuration;
        }

        public async Task<decimal> ConvertPrice(decimal priceGBP, string targetCurrency)
        {
            if (string.Equals(targetCurrency, "GBP", StringComparison.OrdinalIgnoreCase)) return priceGBP;

            if (DateTime.UtcNow > _lastUpdated.AddHours(24))
            {
                await FetchExchangeRates();
            }

            if (_exchangeRates.TryGetValue(targetCurrency.ToUpper(), out var rate))
            {
                // Handle zero-decimal currencies like JPY
                var isZeroDecimal = new[] { "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" }.Contains(targetCurrency.ToUpper());
                return isZeroDecimal ? Math.Round(priceGBP * rate) : Math.Round(priceGBP * rate, 2);
            }

            await _logService.LogWarning("ConvertPrice", $"No exchange rate for {targetCurrency}. Falling back to GBP.");
            return priceGBP;
        }

        private async Task FetchExchangeRates()
        {
            try
            {
                var apiKey = _configuration["ExchangeRateApi:ApiKey"];
                var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"https://v6.exchangerate-api.com/v6/{apiKey}/latest/GBP");

                if (response?.ConversionRates != null)
                {
                    _exchangeRates = response.ConversionRates;
                    _lastUpdated = DateTime.UtcNow;
                    await _logService.LogInfo("FetchExchangeRates", "Exchange rates updated successfully.");
                }
                else
                {
                    await _logService.LogError("FetchExchangeRates", "Failed to fetch exchange rates.");
                    _exchangeRates = new Dictionary<string, decimal>();
                }
            }
            catch (Exception ex)
            {
                await _logService.LogError("FetchExchangeRates Error", ex.Message);
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