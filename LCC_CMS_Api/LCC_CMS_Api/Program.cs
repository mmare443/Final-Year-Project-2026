using Microsoft.EntityFrameworkCore;
using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// Authentication — bearer token (JWT) validation via Entra ID.
// ---------------------------------------------------------------
var authEnabled = builder.Configuration.GetValue<bool>("AuthEnabled", false);

if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(
            builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("StudentOnly", p => p.RequireRole("Student"));
        options.AddPolicy("LecturerOnly", p => p.RequireRole("Lecturer"));
        options.AddPolicy("HoDOnly", p => p.RequireRole("HoD"));
        options.AddPolicy("RegistrarAdminOnly", p => p.RequireRole("RegistrarAdmin"));
        options.AddPolicy("ManagementOnly", p => p.RequireRole("ManagementPrincipal"));
    });
}

// ---------------------------------------------------------------
// Database - EF Core / SQL Server
// ---------------------------------------------------------------
builder.Services.AddDbContext<LccCmsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("LccCmsDb")));

// ---------------------------------------------------------------
// CORS
// ---------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaClient", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
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

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

app.Run();