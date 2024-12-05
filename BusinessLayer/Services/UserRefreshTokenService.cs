
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;

public class UserRefreshTokenService : IUserRefreshTokenService
{
    private readonly CoachingDbContext _context;

    public UserRefreshTokenService(CoachingDbContext context)
    {
        this._context = context;
    }

    public async Task<string> GetRefreshTokenByUserId(string userId)
    {
        try
        {
            var userToken = await _context.UserRefreshTokens
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

            return userToken?.RefreshToken ?? string.Empty;
        }
        catch (Exception ex)
        {

            return string.Empty;
        }
    }

    public async Task<string> GetRefreshTokenByLatest()
    {
        try
        {
            var userToken = await _context.UserRefreshTokens
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

            return userToken?.RefreshToken ?? string.Empty;
        }
        catch (Exception ex)
        {

            return string.Empty;
        }
    }

    public async Task<bool> UpdateGoogleRefreshToken(ModelLayer.Models.UserRefreshToken userRefreshToken)
    {
        try
        {
            _context.UserRefreshTokens.Add(userRefreshToken);

            await _context.SaveChangesAsync();
            
            return true;
        }
        catch (Exception ex)
        {

            return false;
        }
    }
}
