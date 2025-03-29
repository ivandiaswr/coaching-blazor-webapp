using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

public class Contact
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [NotMapped]
    [Required(ErrorMessage = "Please provide your first name.")]
    public string FirstName { get; set; } = string.Empty;
    [NotMapped]
    [Required(ErrorMessage = "Please provide your last name.")]
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please provide a email address.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    //[RegularExpression(@"^[a-zA-Z0-9._%+-]+@gmail\.com$", ErrorMessage = "Only Gmail email addresses are allowed.")]
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
    public bool IsSessionBooking { get; set; } = true;


    /// <summary>
    /// Updates the full name property
    /// </summary>
    public void UpdateFullName()
    {
        FullName = $"{FirstName} {LastName}".Trim();
    }
}
