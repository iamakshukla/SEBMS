using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SEBMS.Api.Services;
using SEBMS.Application.Auth;
using SEBMS.Infrastructure.Identity;

namespace SEBMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<ApplicationUser> users, JwtTokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        return Ok(ToResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        return Ok(ToResponse(user));
    }

    private AuthResponse ToResponse(ApplicationUser user)
    {
        var (token, expiresAt) = tokens.Create(user);
        return new AuthResponse(token, expiresAt, user.Email!, user.DisplayName);
    }
}
