using Microsoft.AspNetCore.Identity;

namespace ModelLayer.Models;

public class UserRefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string RefreshToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public virtual IdentityUser User { get; set; }
}
