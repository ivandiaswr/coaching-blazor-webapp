using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountController(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        var result = await _signInManager.PasswordSignInAsync(
            loginModel.Email,
            loginModel.Password,
            loginModel.RememberMe,
            lockoutOnFailure: false
        );

        if (result.Succeeded)
        {
            return Ok();
        }
        else if (result.IsLockedOut)
        {
            return BadRequest("Your account is locked. Please try again later.");
        }
        else if (result.IsNotAllowed)
        {
            return BadRequest("Login is not allowed. Please confirm your email.");
        }
        else if (result.RequiresTwoFactor)
        {
            return BadRequest("Two-factor authentication required. Please complete the process.");
        }
        else
        {
            return BadRequest("Invalid login attempt.");
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
            return BadRequest(ex);
        }
    }
}
