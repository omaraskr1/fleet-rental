using FleetRental.Application.Abstractions;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetRental.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and puts enough data in place that the app is usable on a
/// fresh clone: an admin to log in as, and a handful of cars to browse.
/// </summary>
public class DbSeeder(
    FleetRentalDbContext db,
    IPasswordHasher passwordHasher,
    ITenantContext tenantContext,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        // Not tenant-owned, so this runs before any tenant is set — same as the
        // tenant itself.
        await SeedPlatformAdminAsync(options, cancellationToken);

        // Seeding creates the tenant it then works inside, so it necessarily starts
        // outside isolation. Once the tenant is set, the rest is filtered normally.
        var tenant = await SeedTenantAsync(options, cancellationToken);
        tenantContext.SetTenant(tenant.Id, tenant.Code);

        await SeedAdminAsync(options, cancellationToken);
        await SeedCarsAsync(cancellationToken);
    }

    /// <summary>
    /// Optional and independent of tenant/admin seeding — a fresh clone is usable
    /// without a platform admin, so this only runs when a password is explicitly
    /// configured, the same "never invent a default" rule <see cref="SeedAdminAsync"/>
    /// follows.
    /// </summary>
    private async Task SeedPlatformAdminAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.PlatformAdminPassword))
        {
            logger.LogWarning("Seed:PlatformAdminPassword is not set; skipping platform-admin seeding.");
            return;
        }

        var email = PlatformAdmin.NormalizeEmail(options.PlatformAdminEmail);

        if (await db.PlatformAdmins.AnyAsync(a => a.Email == email, cancellationToken))
        {
            return;
        }

        db.PlatformAdmins.Add(PlatformAdmin.Create(
            email,
            passwordHasher.Hash(options.PlatformAdminPassword),
            options.PlatformAdminFullName));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded platform administrator {Email}.", email);
    }

    private async Task<Tenant> SeedTenantAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        using var _ = tenantContext.BypassIsolation();

        var code = Tenant.NormalizeCode(options.TenantCode);
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var tenant = Tenant.Create(options.TenantName, code, options.AdminEmail);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded tenant {Name} ({Code}).", tenant.Name, tenant.Code);
        return tenant;
    }

    private async Task SeedAdminAsync(SeedOptions options, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(options.AdminEmail);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

        // The password comes from configuration rather than a constant here, so a
        // deployed environment cannot inherit a well-known default. Program.cs
        // refuses to seed an admin outside Development without one being supplied.
        db.Users.Add(User.RegisterAdmin(
            email,
            passwordHasher.Hash(options.AdminPassword),
            options.AdminFullName));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded fleet administrator {Email}.", email);
    }

    private async Task SeedCarsAsync(CancellationToken cancellationToken)
    {
        if (await db.Cars.AnyAsync(cancellationToken))
        {
            return;
        }

        var cars = new[]
        {
            Create("Mercedes V-Class", "Eight-seat executive van, wrapped for brand activations.",
                CarCategory.Van, 8, 320m,
                "https://images.unsplash.com/photo-1631295868223-63265b40d9e4?w=800"),

            Create("BMW 5 Series", "Executive sedan for VIP transfers and press days.",
                CarCategory.Sedan, 5, 240m,
                "https://images.unsplash.com/photo-1555215695-3004980ad54e?w=800"),

            Create("Range Rover Sport", "Premium SUV, high presence for outdoor activations.",
                CarCategory.Suv, 5, 380m,
                "https://images.unsplash.com/photo-1606664515524-ed2f786a0bd6?w=800"),

            Create("Ford Transit Roadshow", "Purpose-built roadshow truck with fold-out stage panels.",
                CarCategory.BrandedTruck, 3, 450m,
                "https://images.unsplash.com/photo-1600661653561-629509216228?w=800"),

            Create("Mustang Convertible", "Open-top hero car for photo and film shoots.",
                CarCategory.Convertible, 4, 410m,
                "https://images.unsplash.com/photo-1584345604476-8ec5e12e42dd?w=800"),
        };

        db.Cars.AddRange(cars);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} demo cars.", cars.Length);

        static Car Create(string name, string description, CarCategory category, int seats, decimal rate, string photo)
        {
            var car = Car.Create(name, description, category, seats, rate);
            car.AddPhoto(photo, name, isPrimary: true);
            return car;
        }
    }
}

public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Skip seeding entirely, e.g. in production against a managed database.</summary>
    public bool Enabled { get; set; } = true;

    public string AdminEmail { get; set; } = "admin@fleetrental.local";

    /// <summary>
    /// Empty by default on purpose. Development supplies a known value; any other
    /// environment must set one explicitly or admin seeding is skipped.
    /// </summary>
    public string AdminPassword { get; set; } = string.Empty;

    public string AdminFullName { get; set; } = "Fleet Administrator";

    /// <summary>Display name of the first tenant.</summary>
    public string TenantName { get; set; } = "Demo Fleet";

    /// <summary>Company code clients type on first launch.</summary>
    public string TenantCode { get; set; } = "demo-fleet";

    public string PlatformAdminEmail { get; set; } = "platform@fleetrental.local";

    /// <summary>Empty by default, same rule as <see cref="AdminPassword"/>.</summary>
    public string PlatformAdminPassword { get; set; } = string.Empty;

    public string PlatformAdminFullName { get; set; } = "Platform Administrator";
}
