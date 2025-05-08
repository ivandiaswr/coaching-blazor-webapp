using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models
{
    public class SessionPackPrice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public SessionType SessionType { get; set; }
        [Required]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Range(1, int.MaxValue)]
        public int TotalSessions { get; set; }
        [Range(0, double.MaxValue)]
        public decimal PriceEUR { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}