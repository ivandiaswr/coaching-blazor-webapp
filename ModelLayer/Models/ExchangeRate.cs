using System.ComponentModel.DataAnnotations;

namespace ModelLayer.Models
{
    public class ExchangeRate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(3)]
        public string FromCurrency { get; set; } = "GBP";

        [Required]
        [StringLength(3)]
        public string ToCurrency { get; set; } = string.Empty;

        [Required]
        public decimal Rate { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public string Source { get; set; } = "API";

        // Composite unique index on FromCurrency + ToCurrency
        public static string GetCacheKey(string fromCurrency, string toCurrency)
        {
            return $"{fromCurrency.ToUpper()}_{toCurrency.ToUpper()}";
        }
    }
}
