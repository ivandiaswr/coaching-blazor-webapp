using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models
{
    public class UserSubscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int SubscriptionPlanId { get; set; }
        public SubscriptionPlan? Plan { get; set; }
        public string StripeSubscriptionId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int SessionsUsedThisMonth { get; set; } = 0;
        public DateTime? CancelledAt { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
    }
}