using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ModelLayer.Models.Enums;

namespace ModelLayer.Models;

public class Session
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required(ErrorMessage = "Please provide your first name.")]
    public string FirstName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please provide your last name.")]
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please provide a email address.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please select a session.")]
    public SessionType SessionCategory { get; set; }
    public DateTime PreferredDateTime { get; set; }
    [NotMapped]
    [Required(ErrorMessage = "Please select a date and time for your session.")]
    public string PreferredDateTimeString { get; set; } = string.Empty;
    [Required(ErrorMessage = "Let us know what you'd like to discuss in the session.")]
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsSessionBooking { get; set; }
    public bool DiscoveryCall { get; set; }

    // Stripe
    public bool IsPaid { get; set; } = false;
    public string? StripeSessionId { get; set; }
    public DateTime PaidAt { get; set; }

    public string? PackId { get; set; }
    public bool IsPending { get; set; }

    public VideoSession? VideoSession { get; set; }

    public void UpdateFullName()
    {
        FullName = $"{FirstName} {LastName}".Trim();
    }
}
