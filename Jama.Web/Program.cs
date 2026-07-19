using System.Text;
using System.Text.Json.Serialization;
using Jama.Application;
using Jama.Application.Options;
using Jama.Infrastructure;
using Jama.Infrastructure.Data;
using Jama.Web.Infrastructure;
using Jama.Web.Middleware;
using Jama.Web.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Nginx on the same host terminates TLS.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
}

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
        };
    });

builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options =>
{
    options.AddPolicy("JamGoCors", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ??
                ["http://localhost:4200", "https://jamago.qa", "https://www.jamago.qa"])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

try
{
    await app.InitialiseDatabaseAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    // Keep API up when the tunnel/DB is offline, but make schema failures obvious.
    logger.LogError(
        ex,
        "Database initialise failed. Staff/Auth/Contacts will break until MigrateAsync succeeds. " +
        "If tables were created with EnsureCreated before migrations, align schema/history first.");
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("Jama Go API");
    options.AddPreferredSecuritySchemes("Bearer");
    options.AddHttpAuthentication("Bearer", auth =>
    {
        // Leave empty — paste accessToken once in Scalar Auth UI.
        auth.Token = string.Empty;
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("JamGoCors");
app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapEndpoints();

app.Run();
