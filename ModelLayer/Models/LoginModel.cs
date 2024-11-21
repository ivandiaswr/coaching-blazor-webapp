using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class LoginModel {
    // [Key]
    // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    // public string Id { get; set; }
    [Required(ErrorMessage = "Please provide your email.")]
    [EmailAddress(ErrorMessage = "Please provide a valid email address.")]
    public string Email { get; set; }
    [Required(ErrorMessage = "Please provide your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; }
    public bool RememberMe { get; set; }
    public DateTime TimeStampInserted { get; set; } = DateTime.UtcNow;
}