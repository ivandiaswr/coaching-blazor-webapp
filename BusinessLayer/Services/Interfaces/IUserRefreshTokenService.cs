using ModelLayer.Models;

public interface IUserRefreshTokenService {
    Task<bool> UpdateGoogleRefreshToken(UserRefreshToken userRefreshToken);
    Task<string> GetRefreshTokenByUserId(string userId);
}