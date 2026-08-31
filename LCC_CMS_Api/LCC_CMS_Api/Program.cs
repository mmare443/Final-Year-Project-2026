using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------
// Authentication — bearer token (JWT) validation via Entra ID.
// NOTE: This section will throw on startup until real AzureAd values
// are filled in in appsettings.Development.json (see that file's
// comments). Until then, run with AUTH_ENABLED=false (see below) so
// you can build and test controllers without a working Entra ID
// tenant yet.
// ---------------------------------------------------------------
var authEnabled = builder.Configuration.GetValue<bool>("AuthEnabled", false);

if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

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
// Database — EF Core against Azure SQL / local SQL Server.
// NOTE: this only actually connects when a controller calls into
// LccCmsDbContext. The API will start fine with no DB reachable;
// it'll only fail on the first request that touches the database.
// Uncomment once you've scaffolded LccCmsDbContext (see Step 2 of
// the Backend Scaffold Guide v2) — it doesn't exist yet in this
// hand-written starter project.
// ---------------------------------------------------------------
// builder.Services.AddDbContext<LccCmsDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("LccCmsDb")));

// CORS — the React SPA runs on a different origin (localhost:5173) during dev
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

// Serves files placed under wwwroot/ — required for uploaded admissions
// documents (wwwroot/uploads/admissions) to be reachable by URL.
app.UseStaticFiles();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapControllers();

app.Run();
