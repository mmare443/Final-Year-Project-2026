using Microsoft.EntityFrameworkCore;
using LCC_CMS_Api.Models;

namespace LCC_CMS_Api.Services;

public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, LccCmsDbContext dbContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || !IsProtectedApi(context.Request.Path)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userId = ReadUserId(context.User);
        if (userId is null)
        {
            await _next(context);
            return;
        }

        bool mustChange;
        try
        {
            mustChange = await dbContext.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value)
                .Select(u => u.MustChangePassword)
                .FirstOrDefaultAsync(context.RequestAborted);
        }
        catch
        {
            await _next(context);
            return;
        }

        if (!mustChange)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            mustChangePassword = true,
            error = "Password change required before using the portal.",
        });
    }

    private static bool IsProtectedApi(PathString path)
    {
        if (path.StartsWithSegments("/api/account/change-password"))
        {
            return false;
        }

        if (path.StartsWithSegments("/api/me")
            || path.StartsWithSegments("/api/auth")
            || path.StartsWithSegments("/api/health")
            || path.StartsWithSegments("/api/contact"))
        {
            return false;
        }

        return path.StartsWithSegments("/api") || path.StartsWithSegments("/hubs");
    }

    private static int? ReadUserId(System.Security.Claims.ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtTokenService.UserIdClaim)?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return int.TryParse(raw, out var id) && id > 0 ? id : null;
    }
}
