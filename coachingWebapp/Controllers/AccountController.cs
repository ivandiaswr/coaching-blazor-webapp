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
            var user = await _signInManager.UserManager.FindByEmailAsync(loginModel.Email);
            if (user == null)
            {
                await _logService.LogWarning("Login", $"No account found for email: {loginModel.Email}");
                return BadRequest("No account exists with this email.");
            }

            var result = await _signInManager.PasswordSignInAsync(
                loginModel.Email,
                loginModel.Password,
                loginModel.RememberMe,
                lockoutOnFailure: false
            );

            if (result.Succeeded)
            {
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
                await _logService.LogError("Login", $"Account locked out for email: {loginModel.Email}");
                return BadRequest("Your account is locked. Please try again later.");
            }
            else if (result.IsNotAllowed)
            {
                await _logService.LogError("Login", $"Login not allowed for email: {loginModel.Email}");
                return BadRequest("Login is not allowed. Please confirm your email.");
            }
            else if (result.RequiresTwoFactor)
            {
                await _logService.LogError("Login", $"Two-factor authentication required for email: {loginModel.Email}");
                return BadRequest("Two-factor authentication required. Please complete the process.");
            }
            else
            {
                await _logService.LogWarning("Login", $"Invalid password attempt for email: {loginModel.Email}");
                return BadRequest("Invalid password.");
            }
        } 
        catch(Exception ex)
        {
            await _logService.LogError("Login", $"Exception during login for email: {loginModel.Email}. Error: {ex.Message}");
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
