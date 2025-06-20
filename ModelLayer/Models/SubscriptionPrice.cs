using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ModelLayer.Models.Enums;

namespace ModelLayer.Models
{
    public class SubscriptionPrice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public SessionType SessionType { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Range(1, int.MaxValue)]
        public int MonthlyLimit { get; set; }
        [Range(0, double.MaxValue)]
        public decimal PriceGBP { get; set; }
        public decimal? PriceConverted { get; set; }
        public string Currency { get; set; } = "GBP";
        public string? StripePriceId { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}