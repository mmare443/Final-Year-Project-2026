using LCC_CMS_Api.Models;
using LCC_CMS_Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const string Category = "profiles";

    private readonly LccCmsDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _fileStorage;

    public ProfileController(
        LccCmsDbContext dbContext,
        ICurrentUser currentUser,
        IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    [HttpGet("photo")]
    public async Task<IActionResult> GetPhoto(CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        if (user is null) return Unauthorized();

        return await OpenPhotoAsync(user.ProfilePhotoUrl, cancellationToken);
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpGet("photo/{userId:int}")]
    public async Task<IActionResult> GetPhotoForUser(int userId, CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is null)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null) return NotFound();

        return await OpenPhotoAsync(user.ProfilePhotoUrl, cancellationToken);
    }

    [HttpPost("photo")]
    [RequestSizeLimit(MaxPhotoBytes + 1024)]
    public async Task<ActionResult<ProfilePhotoRecord>> UploadPhoto(
        IFormFile? photo,
        CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        if (user is null) return Unauthorized();

        if (photo is null || photo.Length == 0)
        {
            return BadRequest("No photo file received.");
        }

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        if (ext is not (".jpg" or ".png"))
        {
            return BadRequest("File type not allowed. Use JPG or PNG.");
        }

        if (photo.Length > MaxPhotoBytes)
        {
            return BadRequest("Photo is too large. Maximum size is 5 MB.");
        }

        var fileName = $"user-{user.UserId}{ext}";
        await using var input = photo.OpenReadStream();
        var stored = await _fileStorage.SaveAsAsync(
            input,
            Category,
            fileName,
            photo.FileName,
            photo.ContentType,
            cancellationToken);

        user.ProfilePhotoUrl = $"uploads/{stored.StorageKey}";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ProfilePhotoRecord
        {
            UserId = user.UserId,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
        });
    }

    private async Task<User?> LoadUserAsync(CancellationToken cancellationToken)
    {
        if (!await _currentUser.ResolveAsync(cancellationToken) || _currentUser.UserId is not int userId)
        {
            return null;
        }

        return await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    private async Task<IActionResult> OpenPhotoAsync(string? profilePhotoUrl, CancellationToken cancellationToken)
    {
        var storageKey = ToStorageKey(profilePhotoUrl);
        if (string.IsNullOrWhiteSpace(storageKey)) return NotFound();

        Stream content;
        try
        {
            content = await _fileStorage.OpenReadAsync(storageKey, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        return File(content, ContentTypeFor(storageKey), Path.GetFileName(storageKey), enableRangeProcessing: true);
    }

    private static string? ToStorageKey(string? profilePhotoUrl)
    {
        if (string.IsNullOrWhiteSpace(profilePhotoUrl)) return null;
        var value = profilePhotoUrl.Replace('\\', '/').Trim().TrimStart('/');
        if (value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["uploads/".Length..];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ContentTypeFor(string storageKey)
    {
        return Path.GetExtension(storageKey).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };
    }
}

public class ProfilePhotoRecord
{
    public int UserId { get; set; }
    public string? ProfilePhotoUrl { get; set; }
}
