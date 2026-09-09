using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly JwtTokenService _tokens;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        LccCmsDbContext dbContext,
        JwtTokenService tokens,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim() ?? "";
        var password = request.Password ?? "";
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest("Email and password are required.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Email == email,
                cancellationToken);

        if (user is null
            || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(user.PasswordHash)
            || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
                == PasswordVerificationResult.Failed)
        {
            _logger.LogInformation("Login failed for {Email}", email);
            return Unauthorized("Invalid email or password.");
        }

        var expires = DateTime.UtcNow.AddMinutes(
            _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 480);
        var token = _tokens.CreateToken(user, expires);
        var role = RoleNames.ToPolicyRole(user.Role);

        _logger.LogInformation(
            "Login succeeded. UserId={UserId} Role={Role}",
            user.UserId,
            role);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.UserId,
            Email = user.Email,
            Role = role,
            ExpiresAt = expires,
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
