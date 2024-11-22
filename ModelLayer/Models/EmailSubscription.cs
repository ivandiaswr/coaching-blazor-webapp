using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

[Table("EmailSubscriptions")]
public class EmailSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required(ErrorMessage = "Please provide a email address.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string Email { get; set; } = string.Empty;
    public GiftCategory Gift { get; set; }
    public DateTime SubscribedAt { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
