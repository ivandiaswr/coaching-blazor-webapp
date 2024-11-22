using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

public class Contact
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required(ErrorMessage = "Please provide your name.")]
    public string Name { get; set; } = string.Empty;
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
}
