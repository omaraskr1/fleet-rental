using FleetRental.Application.Abstractions;
using FleetRental.Infrastructure.Notifications;
using FleetRental.Infrastructure.Persistence;
using FleetRental.Infrastructure.Security;
using FleetRental.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetRental.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FleetRental")
            ?? throw new InvalidOperationException(
                "Connection string 'FleetRental' is missing. Set it in appsettings.json or via " +
                "ConnectionStrings__FleetRental in the environment.");

        services.AddDbContext<FleetRentalDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Retries around transient faults. The transaction wrapper in
                // FleetRentalDbContext is built to cooperate with this — it takes
                // the execution strategy's ownership of the retry into account.
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
                sql.MigrationsAssembly(typeof(FleetRentalDbContext).Assembly.FullName);
            }));

        // Application depends on the interface; this is the only place the concrete
        // context is handed over.
        services.AddScoped<IFleetRentalDbContext>(sp => sp.GetRequiredService<FleetRentalDbContext>());

        // Scoped: one tenant per request, resolved by TenantResolutionMiddleware
        // and read by both the query filters and save-time tenant assignment.
        services.AddScoped<ITenantContext, TenantContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

        // Registered as a collection: BookingNotificationService injects
        // IEnumerable<INotificationSender> and fans out across all of them, so
        // Phase 2's WhatsApp channel is one more line here.
        services.AddScoped<INotificationSender, EmailNotificationSender>();
        services.AddScoped<INotificationSender, PushNotificationSender>();

        services.AddScoped<DbSeeder>();

        return services;
    }
}
