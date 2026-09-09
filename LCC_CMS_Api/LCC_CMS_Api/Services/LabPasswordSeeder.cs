using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LCC_CMS_Api.Models;
using Microsoft.Extensions.Options;

namespace LCC_CMS_Api.Services;

public static class LabPasswordSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<JwtSettings>>().Value;
        if (string.IsNullOrWhiteSpace(settings.LabPassword))
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<LccCmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        List<User> users;
        try
        {
            users = await db.Users
                .Where(u => u.PasswordHash == null && u.Status == "Active")
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not seed lab passwords. Run database/LCC_CMS_Schema_Upgrade_Rev5_LocalAuth.sql to add users.password_hash.");
            return;
        }

        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            user.PasswordHash = hasher.HashPassword(user, settings.LabPassword);
        }

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Seeded lab password hashes for {Count} user(s). Lab password is JwtSettings:LabPassword.",
            users.Count);
    }
}
