using System.ComponentModel.DataAnnotations;

namespace ModelLayer.Models
{
    public class ForgotPassword
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}