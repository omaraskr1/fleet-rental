using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FleetRental.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time. It lets migrations be generated
/// against the Infrastructure project directly, without spinning up the API host
/// or needing a reachable database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FleetRentalDbContext>
{
    public FleetRentalDbContext CreateDbContext(string[] args)
    {
        // Override with FLEETRENTAL_CONNECTION to target a different instance.
        // The runtime connection string comes from configuration, not from here.
        var connectionString = Environment.GetEnvironmentVariable("FLEETRENTAL_CONNECTION")
            ?? "Server=.\\SQLEXPRESS;Database=FleetRental;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<FleetRentalDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FleetRentalDbContext(options);
    }
}
