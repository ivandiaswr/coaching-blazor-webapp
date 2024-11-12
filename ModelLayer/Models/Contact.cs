using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelLayer.Models;

public class Contact
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Required(ErrorMessage = "Name is required")]
    public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;
    [Required(ErrorMessage = "Please select a session type")]
    public SessionType SessionCategory { get; set; } 
     [Required(ErrorMessage = "Preferred date and time are required")]
    public DateTime PreferredDateTime { get; set; }
    [Required(ErrorMessage = "Please select a time zone")]
    public string TimeZone { get; set; } = "UTC";
    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime TimeStampInserted { get; set; }
}
