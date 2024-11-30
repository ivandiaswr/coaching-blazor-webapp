public class TokenResponse
{
    public string Email { get; set; }
    public string RefreshToken { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string Scope { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string TokenType { get; set; }
}
