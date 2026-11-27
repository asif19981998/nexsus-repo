using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Nexus.API.Controllers;
using Nexus.API.Security;
using Nexus.API.Storage;
using Nexus.Application;
using Nexus.Application.Common;
using Nexus.BuildingBlocks.Exceptions.Handler;
using Nexus.Infrastructure;
using Nexus.Infrastructure.Identity;
using Nexus.Infrastructure.Seeders;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration);

builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// ---------- authentication: JWT bearer ----------
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Set Jwt:Key to a secret of at least 32 characters (appsettings or the Jwt__Key environment variable).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });

// ---------- authorization: permissions from the RolePermission table ----------
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    // Every endpoint needs a signed-in user unless it says [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// ---------- CORS for the Next.js frontend ----------
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- rate limits: anonymous sign-up, per client IP ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "TooManyRequests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Too many sign-up attempts from this network. Try again in a few minutes.",
        }, ct));
    };
    options.AddPolicy(AuthController.RegisterLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexus UGC Internship API", Version = "v1" });
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/v1/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
});

var app = builder.Build();

// Migrate and seed before taking requests.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.UseExceptionHandler();

// Uploaded files (attendance selfies) are kept in <content root>/files and served from /files.
var filesRoot = Path.Combine(app.Environment.ContentRootPath, "files");
Directory.CreateDirectory(filesRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(filesRoot),
    RequestPath = "/files",
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
