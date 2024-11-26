using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

public class SecurityService : ISecurityService
{
    private readonly IConfiguration _configuration;
    public SecurityService(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    public string GenerateUnsubscribeToken(string email)
    {
        byte[] key = Encoding.ASCII.GetBytes(_configuration["AppSettings:SecretKey"]);

        // HMACSHA256 is a hashing algorithm that provides data integrity and authentication
        // Combines the secret key with the user's email to produce a hash that is secure against tampering
        using (var hmac = new HMACSHA256(key)) 
        {
            // Converts the email address into a byte array and computes its hash using the HMACSHA256 algorithm
            var tokenBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(email));
            return Convert.ToBase64String(tokenBytes);
        }
    }

    public bool ValidateUnsubscribeToken(string email, string token)
    {
        string expectedToken = GenerateUnsubscribeToken(email);
        return token == expectedToken;
    }
}