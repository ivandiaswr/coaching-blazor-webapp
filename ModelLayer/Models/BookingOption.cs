using ModelLayer.Models.Enums;

namespace ModelLayer.Models
{
    public class BookingOption
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? PriceEUR { get; set; }
        public BookingType Type { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool RequiresPurchase { get; set; } = false;
        public int? PlanId { get; set; }
    }
}