namespace BusinessLayer.Services.Interfaces
{
    public interface ICurrencyConversionService
    {
        Task<decimal> ConvertPrice(decimal priceGBP, string targetCurrency);
        Task FetchExchangeRates();
    }
}