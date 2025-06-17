namespace BusinessLayer.Services.Interfaces
{
    public interface ICurrencyConversionService
    {
        Task<(decimal Price, string Error)> ConvertPrice(decimal priceGBP, string targetCurrency);
        Task FetchExchangeRates();
    }
}