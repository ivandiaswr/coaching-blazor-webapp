namespace ModelLayer.Models.DTOs
{
    public class SubscriptionStatusDto
    {
        public bool HasActiveSubscription { get; set; }
        public int MonthlyLimit { get; set; }
        public int SessionsUsed { get; set; }
        public int Remaining => MonthlyLimit - SessionsUsed;
        public string PlanId { get; set; }
    }
}