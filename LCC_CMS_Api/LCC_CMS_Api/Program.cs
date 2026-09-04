using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// Authentication — bearer token (JWT) validation via Entra ID.
// ---------------------------------------------------------------
var authEnabled = builder.Configuration.GetValue<bool>("AuthEnabled", false);

// JWT is registered even when AuthEnabled=false so GET /api/me can be
// tested with a real Entra token. [Authorize] stays commented; there is
// no fallback policy. Lab still uses X-User-Id when no token is present.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    options.TokenValidationParameters.NameClaimType = "preferred_username";
    var audience = builder.Configuration["AzureAd:Audience"];
    if (!string.IsNullOrWhiteSpace(audience))
    {
        options.TokenValidationParameters.ValidAudience = audience;
    }

    options.Events ??= new JwtBearerEvents();
    var previousMessage = options.Events.OnMessageReceived;
    options.Events.OnMessageReceived = async context =>
    {
        var accessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken)
            && context.HttpContext.Request.Path.StartsWithSegments("/hubs/messages"))
        {
            context.Token = accessToken;
        }

        if (previousMessage is not null)
        {
            await previousMessage(context);
        }
    };

    var previousValidated = options.Events.OnTokenValidated;
    options.Events.OnTokenValidated = async context =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("LCC_CMS_Api.Identity");
        var principal = context.Principal;
        var oid = principal?.FindFirstValue(ClaimConstants.ObjectId)
            ?? principal?.FindFirstValue("oid")
            ?? principal?.FindFirstValue("sub");
        var roles = principal is null
            ? new List<string>()
            : principal.FindAll(ClaimTypes.Role)
                .Concat(principal.FindAll("roles"))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        logger.LogInformation(
            "JWT validated. Oid={Oid} Roles={Roles}",
            oid ?? "(none)",
            roles.Count == 0 ? "(none)" : string.Join(",", roles));

        if (previousValidated is not null)
        {
            await previousValidated(context);
        }
    };
});

builder.Services.AddScoped<IClaimsTransformation, LCC_CMS_Api.Services.EntraRoleClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", p => p.RequireRole(
        LCC_CMS_Api.Services.RoleNames.Student));
    options.AddPolicy("LecturerOnly", p => p.RequireRole(
        LCC_CMS_Api.Services.RoleNames.Lecturer));
    options.AddPolicy("HoDOnly", p => p.RequireRole(
        LCC_CMS_Api.Services.RoleNames.HoD));
    options.AddPolicy("RegistrarAdminOnly", p => p.RequireRole(
        LCC_CMS_Api.Services.RoleNames.RegistrarAdmin,
        LCC_CMS_Api.Services.RoleNames.RegistrarAdminSql));
    options.AddPolicy("ManagementOnly", p => p.RequireRole(
        LCC_CMS_Api.Services.RoleNames.ManagementPrincipal,
        LCC_CMS_Api.Services.RoleNames.ManagementPrincipalSql));
});

// ---------------------------------------------------------------
// Database - EF Core / SQL Server
// ---------------------------------------------------------------
builder.Services.AddDbContext<LccCmsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("LccCmsDb")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LCC_CMS_Api.Services.ICurrentUser, LCC_CMS_Api.Services.CurrentUserService>();
builder.Services.AddScoped<LCC_CMS_Api.Services.CourseResultService>();
builder.Services.AddSingleton<LCC_CMS_Api.Services.IEntraUserProvisioner, LCC_CMS_Api.Services.GraphEntraUserProvisioner>();

// ---------------------------------------------------------------
// CORS
// ---------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaClient", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("SpaClient");

// Static files
app.UseStaticFiles();

app.UseAuthentication();
if (authEnabled)
{
    app.UseAuthorization();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hubs/messages"))
    {
        var currentUser = context.RequestServices.GetRequiredService<LCC_CMS_Api.Services.ICurrentUser>();
        if (!await currentUser.ResolveAsync(context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.MapControllers();
app.MapHub<LCC_CMS_Api.Hubs.MessageHub>("/hubs/messages");

app.Run();