using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

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

        var row = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => new
            {
                u.UserId,
                u.Email,
                u.PasswordHash,
                u.Status,
                u.Role,
                u.EntraId,
                u.MustChangePassword,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null
            || !string.Equals(row.Status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(row.PasswordHash))
        {
            _logger.LogInformation("Login failed for {Email}", email);
            return Unauthorized("Invalid email or password.");
        }

        var user = new User
        {
            UserId = row.UserId,
            Email = row.Email,
            PasswordHash = row.PasswordHash,
            Status = row.Status,
            Role = row.Role,
            EntraId = row.EntraId,
        };

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
            == PasswordVerificationResult.Failed)
        {
            _logger.LogInformation("Login failed for {Email}", email);
            return Unauthorized("Invalid email or password.");
        }

        var expires = DateTime.UtcNow.AddMinutes(
            _jwtSettings.ExpiryMinutes > 0 ? _jwtSettings.ExpiryMinutes : 480);
        var token = _tokens.CreateToken(user, expires);
        var role = RoleNames.ToPolicyRole(user.Role);

        var mustChange = row.MustChangePassword
            || PasswordPolicy.EqualsTemporary(password, _jwtSettings.LabPassword);

        _logger.LogInformation(
            "Login succeeded. UserId={UserId} Role={Role} MustChangePassword={MustChange}",
            user.UserId,
            role,
            mustChange);

        return Ok(new LoginResponse
        {
            LoginSuccess = true,
            MustChangePassword = mustChange,
            Token = token,
            UserId = user.UserId,
            Email = user.Email,
            Role = role,
            ExpiresAt = expires,
        });
    }

    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<ActionResult<ActivateResponse>> Activate(
        [FromBody] ActivateRequest request,
        CancellationToken cancellationToken)
    {
        var token = request.Token?.Trim() ?? "";
        var password = request.Password ?? "";
        var confirm = request.ConfirmPassword ?? "";

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Activation token is required.");
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            return BadRequest("Password and confirmation do not match.");
        }

        var policyError = PasswordPolicy.Validate(password);
        if (policyError is not null)
        {
            return BadRequest(policyError);
        }

        if (PasswordPolicy.EqualsTemporary(password, _jwtSettings.LabPassword))
        {
            return BadRequest("Password cannot be the temporary laboratory password.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.ActivationToken == token, cancellationToken);

        if (user is null)
        {
            return BadRequest("This activation link is not valid.");
        }

        if (user.ActivationUsed)
        {
            return BadRequest("This activation link has already been used.");
        }

        if (user.ActivationExpiresAt is null
            || user.ActivationExpiresAt.Value <= DateTime.UtcNow)
        {
            return BadRequest("This activation link has expired.");
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("This account cannot be activated.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.ActivationUsed = true;
        user.MustChangePassword = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Account activated. UserId={UserId}", user.UserId);

        return Ok(new ActivateResponse
        {
            Success = true,
            Message = "Password set. You can now sign in.",
            Email = user.Email,
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
    [JsonPropertyName("loginSuccess")]
    public bool LoginSuccess { get; set; }

    [JsonPropertyName("mustChangePassword")]
    public bool MustChangePassword { get; set; }

    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class ActivateRequest
{
    public string Token { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class ActivateResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string Email { get; set; } = "";
}
