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
        public decimal PriceEUR { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}