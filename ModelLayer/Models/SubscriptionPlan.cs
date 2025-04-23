using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models
{
    public class SubscriptionPlan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MonthlySessionLimit { get; set; }
        public decimal PriceEUR { get; set; }
        public string? Description { get; set; }
        public string StripePriceId { get; set; } = string.Empty; 
    }
}