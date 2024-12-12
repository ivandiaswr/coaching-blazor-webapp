
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;

public class UserRefreshTokenService : IUserRefreshTokenService
{
    private readonly CoachingDbContext _context;
    private readonly ILogService _logService;

    public UserRefreshTokenService(CoachingDbContext context, ILogService logService)
    {
        this._context = context;
        this._logService = logService;
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
            _logService.LogError("GetRefreshTokenByUserId", ex.Message);
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
            _logService.LogError("GetRefreshTokenByLatest", ex.Message);
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
            _logService.LogError("UpdateGoogleRefreshToken", ex.Message);
            return false;
        }
    }
}
