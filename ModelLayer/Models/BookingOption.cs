using ModelLayer.Models.Enums;

namespace ModelLayer.Models
{
    public class BookingOption
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? PriceGBP { get; set; }
        public decimal? PriceConverted { get; set; }
        public string Currency { get; set; } = "GBP";
        public BookingType Type { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool RequiresPurchase { get; set; } = false;
        public string? PlanId { get; set; }
        public int? PackId { get; set; }
        public int? TotalSessions { get; set; }
        public int? MonthlyLimit { get; set; }
    }
}