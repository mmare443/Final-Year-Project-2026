using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AccountController> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var currentPassword = request.CurrentPassword ?? "";
        var newPassword = request.NewPassword ?? "";
        var confirmPassword = request.ConfirmPassword ?? "";

        if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
        {
            return BadRequest("Current password and new password are required.");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return BadRequest("New password and confirmation do not match.");
        }

        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError is not null)
        {
            return BadRequest(policyError);
        }

        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            return BadRequest("New password must be different from the current password.");
        }

        if (PasswordPolicy.EqualsTemporary(newPassword, _jwtSettings.LabPassword))
        {
            return BadRequest("New password cannot be the temporary laboratory password.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            return Unauthorized();
        }

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword)
            == PasswordVerificationResult.Failed)
        {
            return BadRequest("Current password is not correct.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed. UserId={UserId}", user.UserId);

        return Ok(new ChangePasswordResponse
        {
            Success = true,
            MustChangePassword = false,
            Message = "Password updated. You can continue to the portal.",
        });
    }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public bool MustChangePassword { get; set; }
    public string Message { get; set; } = "";
}
