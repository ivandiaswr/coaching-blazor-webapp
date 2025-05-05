namespace ModelLayer.Models
{
    public class SubscriptionStatus
    {
        public bool HasActiveSubscription { get; set; }
        public int Remaining { get; set; }
        public int SessionsUsed { get; set; }
        public int MonthlyLimit { get; set; }
        public string PlanId { get; set; }
    }
}