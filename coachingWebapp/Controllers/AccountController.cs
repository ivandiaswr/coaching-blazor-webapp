using BusinessLayer.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ModelLayer.Models;

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogService _logService;

    public AccountController(SignInManager<ApplicationUser> signInManager, ILogService logService)
    {
        _signInManager = signInManager;
        this._logService = logService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        try
        {
            var result = await _signInManager.PasswordSignInAsync(
                loginModel.Email,
                loginModel.Password,
                loginModel.RememberMe,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
            {
                var user = await _signInManager.UserManager.FindByEmailAsync(loginModel.Email);
                var principal = await _signInManager.CreateUserPrincipalAsync(user);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = loginModel.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    });

                var roles = await _signInManager.UserManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "User";

                return Ok(new { success = true, role });
            }
            else if (result.IsLockedOut)
            {
                await _logService.LogError("Login", "IsLockedOut");
                return BadRequest("Your account is locked. Please try again later.");
            }
            else if (result.IsNotAllowed)
            {
                await _logService.LogError("Login", "IsNotAllowed");
                return BadRequest("Login is not allowed. Please confirm your email.");
            }
            else if (result.RequiresTwoFactor)
            {
                await _logService.LogError("Login", "RequiresTwoFactor");
                return BadRequest("Two-factor authentication required. Please complete the process.");
            }
            else
            {
                await _logService.LogError("Login", $"Global error invalid login attempt");
                return BadRequest("Invalid login attempt.");
            }
        } 
        catch(Exception ex)
        {
            await _logService.LogError("Login", ex.Message);
            return Problem(title: "Login failed", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _signInManager.SignOutAsync();

            return Ok();
        } 
        catch(Exception ex)
        {
            await _logService.LogError("Logout", ex.Message);
            return BadRequest(ex);
        }
    }
}
