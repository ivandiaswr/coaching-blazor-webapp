using ModelLayer.Models.Enums;

namespace ModelLayer.Models
{
    public class CheckoutSessionRequest
    {
        public Session Session { get; set; }
        public BookingType BookingType { get; set; }
        public string PlanId { get; set; }
    }
}