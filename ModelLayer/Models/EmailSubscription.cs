using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

[Table("EmailSubscriptions")]
public class EmailSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;
    public DateTime SubscribedAt { get; set; }
    public bool IsSubscribed { get; set; }
    public DateTime? UnsubscribedAt { get; set; }
}
