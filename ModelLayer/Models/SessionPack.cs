using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ModelLayer.Models;

namespace ModelLayer
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

        public int DefinitionId { get; set; }
        public SessionPackDefinition Definition { get; set; }
    }
}