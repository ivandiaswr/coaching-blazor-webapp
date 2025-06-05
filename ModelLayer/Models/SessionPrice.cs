using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models
{
    public class SessionPrice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public SessionType SessionType { get; set; }
        public decimal PriceGBP { get; set; }
        public string Currency { get; set; } = "GBP";
        public DateTime LastUpdated { get; set; }
    }
}