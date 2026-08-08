using FleetRental.Application.Abstractions;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using FleetRental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Boots the real API pipeline against a dedicated SQL Server test database.
/// </summary>
/// <remarks>
/// Deliberately NOT EF InMemory. The single most important guarantee in this
/// system — that a unique index makes double-booking impossible — does not exist
/// in the InMemory provider, which ignores indexes entirely. A suite that used it
/// would pass while the guarantee was broken, which is worse than no suite.
/// </remarks>
public class FleetRentalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Local SQL Express by default; CI overrides it with FLEETRENTAL_TEST_CONNECTION
    /// to point at the SQL Server service container, which uses SQL auth rather
    /// than Windows integrated security.
    /// </summary>
    private static readonly string TestConnectionString =
        Environment.GetEnvironmentVariable("FLEETRENTAL_TEST_CONNECTION")
        ?? "Server=.\\SQLEXPRESS;Database=FleetRental_Test;Trusted_Connection=True;TrustServerCertificate=True";

    /// <summary>
    /// Tenant used by tests that are not themselves about tenancy. They still run
    /// inside one, because after this change every request does.
    /// </summary>
    public const string DefaultTenantCode = "test-fleet";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting("ConnectionStrings:FleetRental", TestConnectionString);

        // A 32+ character key, required or startup throws by design.
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-not-used-anywhere-else-0123456789");

        // Tests seed exactly what they need; startup seeding would make arrange
        // steps depend on data they did not create.
        builder.UseSetting("Seed:Enabled", "false");

        // Keeps notification output from flooding the test log.
        builder.UseSetting("Email:Enabled", "false");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();

        // Recreated from migrations so the schema under test is exactly the one
        // that ships — including UX_BookedDays_Car_Date and UX_Users_Tenant_Email.
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// An HTTP client scoped to a tenant via the company-code header — the same
    /// path a real client app takes on first launch.
    /// </summary>
    public ApiClient CreateTenantClient(string tenantCode = DefaultTenantCode)
    {
        var http = CreateClient();
        http.DefaultRequestHeaders.Add("X-Tenant-Code", tenantCode);
        return new ApiClient(http);
    }

    /// <summary>Clears all data between tests, keeping the schema.</summary>
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();

        // Order matters — children before parents, or the FKs reject the delete.
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM BookedDays;
            DELETE FROM Bookings;
            DELETE FROM Events;
            DELETE FROM DeviceTokens;
            DELETE FROM CarPhotos;
            DELETE FROM ServiceRecords;
            DELETE FROM VehicleIssues;
            DELETE FROM Cars;
            DELETE FROM Users;
            DELETE FROM Tenants;
            DELETE FROM PlatformAdmins;
            """);
    }

    /// <summary>Creates a tenant and returns its id.</summary>
    public async Task<Guid> SeedTenantAsync(string code = DefaultTenantCode, string? name = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using var _ = tenantContext.BypassIsolation();

        var normalized = Tenant.NormalizeCode(code);
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Code == normalized);

        if (existing is not null)
        {
            return existing.Id;
        }

        var tenant = Tenant.Create(name ?? $"{code} Fleet", normalized);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return tenant.Id;
    }

    public async Task SuspendTenantAsync(Guid tenantId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using var _ = tenantContext.BypassIsolation();

        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.Suspend();
        await db.SaveChangesAsync();
    }

    /// <summary>A plain HTTP client with no tenant header — platform endpoints need none.</summary>
    public ApiClient CreatePlatformClient() => new(CreateClient());

    /// <summary>Creates a platform admin (not tenant-owned) and returns their credentials.</summary>
    public async Task<(string Email, string Password)> SeedPlatformAdminAsync(
        string email = "platform@test.local",
        string password = "PlatformPass123")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.PlatformAdmins.Add(PlatformAdmin.Create(email, hasher.Hash(password), "Test Platform Admin"));
        await db.SaveChangesAsync();

        return (email, password);
    }

    /// <summary>Creates an administrator inside a tenant and returns their credentials.</summary>
    public async Task<(string Email, string Password)> SeedAdminAsync(
        Guid? tenantId = null,
        string email = "admin@test.local",
        string password = "AdminPass123")
    {
        var tenant = tenantId ?? await SeedTenantAsync();

        await InTenantScopeAsync(tenant, (db, hasher) =>
        {
            db.Users.Add(User.RegisterAdmin(email, hasher.Hash(password), "Test Admin"));
            return Task.CompletedTask;
        });

        return (email, password);
    }

    /// <summary>
    /// Creates a bookable car inside a tenant and returns its id. The category
    /// defaults to Van so existing tests are unaffected; the per-category demand
    /// tests are the reason it can be varied at all.
    /// </summary>
    public async Task<Guid> SeedCarAsync(
        Guid? tenantId = null,
        string name = "Test Car",
        CarStatus status = CarStatus.Active,
        CarCategory category = CarCategory.Van,
        decimal rate = 300m,
        PricingModel pricingModel = PricingModel.PerDay)
    {
        var tenant = tenantId ?? await SeedTenantAsync();
        Guid carId = Guid.Empty;

        await InTenantScopeAsync(tenant, (db, _) =>
        {
            var car = Car.Create(name, "Seeded for tests", category, 8, rate, pricingModel: pricingModel);
            car.AddPhoto("https://example.com/car.jpg", name, isPrimary: true);

            if (status != CarStatus.Active)
            {
                car.ChangeStatus(status);
            }

            db.Cars.Add(car);
            carId = car.Id;
            return Task.CompletedTask;
        });

        return carId;
    }

    /// <summary>Soft-disables a platform admin so the login path can be exercised.</summary>
    public async Task DeactivatePlatformAdminAsync(string email)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();

        var normalized = PlatformAdmin.NormalizeEmail(email);
        var admin = await db.PlatformAdmins.SingleAsync(a => a.Email == normalized);

        admin.Deactivate();
        await db.SaveChangesAsync();
    }

    /// <summary>Soft-disables an account so the login path can be exercised.</summary>
    public async Task DeactivateUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        using var _ = tenantContext.BypassIsolation();

        var normalized = User.NormalizeEmail(email);
        var user = await db.Users.SingleAsync(u => u.Email == normalized);

        user.Deactivate();
        await db.SaveChangesAsync();
    }

    /// <summary>Counts held days for a car — the ground truth for availability.</summary>
    public async Task<int> CountBookedDaysAsync(Guid carId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // Bypassed deliberately: assertions must observe what is actually in the
        // table, not what the tenant filter would let a request see. Otherwise a
        // broken filter could make a leak test pass by hiding the evidence.
        using var _ = tenantContext.BypassIsolation();

        return await db.BookedDays.CountAsync(d => d.CarId == carId);
    }

    /// <summary>
    /// Runs seeding work as if inside a request for the given tenant, so the
    /// save-time tenant assignment stamps rows exactly as production would.
    /// </summary>
    private async Task InTenantScopeAsync(
        Guid tenantId,
        Func<FleetRentalDbContext, IPasswordHasher, Task> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        tenantContext.SetTenant(tenantId, "seed");

        await work(db, hasher);
        await db.SaveChangesAsync();
    }
}
