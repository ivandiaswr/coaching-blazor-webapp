using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models
{
    public class SessionPack
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string UserId { get; set; }
        public int SessionsRemaining { get; set; }
        public DateTime PurchasedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public int PriceId { get; set; }
        public SessionPackPrice Price { get; set; }
    }
}