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
        // that ships — including UX_BookedDays_Car_Date.
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

    /// <summary>Clears transactional data between tests, keeping the schema.</summary>
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
            DELETE FROM Cars;
            DELETE FROM Users;
            """);
    }

    /// <summary>Creates an administrator and returns their credentials.</summary>
    public async Task<(string Email, string Password)> SeedAdminAsync(
        string email = "admin@test.local",
        string password = "AdminPass123")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Users.Add(User.RegisterAdmin(email, hasher.Hash(password), "Test Admin"));
        await db.SaveChangesAsync();

        return (email, password);
    }

    /// <summary>Creates a bookable car and returns its id.</summary>
    public async Task<Guid> SeedCarAsync(string name = "Test Car", CarStatus status = CarStatus.Active)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();

        var car = Car.Create(name, "Seeded for tests", CarCategory.Van, 8, 300m);
        car.AddPhoto("https://example.com/car.jpg", name, isPrimary: true);

        if (status != CarStatus.Active)
        {
            car.ChangeStatus(status);
        }

        db.Cars.Add(car);
        await db.SaveChangesAsync();

        return car.Id;
    }

    /// <summary>Soft-disables an account so the login path can be exercised.</summary>
    public async Task DeactivateUserAsync(string email)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetRentalDbContext>();

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
        return await db.BookedDays.CountAsync(d => d.CarId == carId);
    }
}
