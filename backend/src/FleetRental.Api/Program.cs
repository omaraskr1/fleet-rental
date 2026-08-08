using System.Text;
using System.Text.Json.Serialization;
using FleetRental.Api.Middleware;
using FleetRental.Application;
using FleetRental.Infrastructure;
using FleetRental.Infrastructure.Persistence;
using FleetRental.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as strings so the Angular client can switch on
        // "Approved" rather than on a number that shifts if the enum is reordered.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Auth ----------

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.Key) || jwtOptions.Key.Length < 32)
{
    // Refuse to start rather than sign tokens with a weak or absent key. A
    // misconfigured deploy should fail loudly here, not issue forgeable tokens.
    throw new InvalidOperationException(
        "Jwt:Key must be set to at least 32 characters. Supply it via user-secrets " +
        "(dotnet user-secrets set \"Jwt:Key\" \"...\") or the Jwt__Key environment variable.");
}

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
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),

            // Default is 5 minutes of leeway on expiry; that is more than this app wants.
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// ---------- CORS ----------

const string CorsPolicy = "FleetRentalClients";

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        // The Ionic dev server, the admin panel, and Capacitor's native origins.
        // Capacitor serves the app from capacitor:// on iOS and http://localhost
        // on Android, which is why both appear here.
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ??
            [
                "http://localhost:4200",
                "http://localhost:8100",
                "capacitor://localhost",
                "ionic://localhost",
            ];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ---------- OpenAPI ----------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fleet Rental API",
        Version = "v1",
        Description = "Booking platform for a marketing/event rental fleet.",
    });

    // Lets the Swagger UI carry the bearer token, so the admin endpoints are
    // testable without an external client.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by /api/auth/login.",
    });

    // Microsoft.OpenApi 2.x replaced the old `Reference = new OpenApiReference{...}`
    // shape with dedicated reference types, and Swashbuckle 10.x takes a factory so
    // the reference can be bound to the document being generated.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var app = builder.Build();

// First in the pipeline so it catches everything downstream.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fleet Rental API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();

// Must run AFTER authentication so the JWT tenant claim is available, and BEFORE
// authorization and the controllers so no query executes without a tenant filter.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();
app.MapControllers();

// ---------- Startup migration + seed ----------

await using (var scope = app.Services.CreateAsyncScope())
{
    var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

    if (seedOptions.Enabled)
    {
        if (string.IsNullOrWhiteSpace(seedOptions.AdminPassword))
        {
            // Never invent a default admin password — an unattended deploy would
            // then ship with known credentials.
            scope.ServiceProvider
                .GetRequiredService<ILogger<Program>>()
                .LogWarning("Seed:AdminPassword is not set; skipping administrator seeding.");
        }
        else
        {
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(seedOptions);
        }
    }
}

app.Run();

/// <summary>Exposed so integration tests can drive the real pipeline via WebApplicationFactory.</summary>
public partial class Program;
