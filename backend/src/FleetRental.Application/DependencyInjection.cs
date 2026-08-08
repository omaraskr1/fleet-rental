using FleetRental.Application.Analytics;
using FleetRental.Application.Auth;
using FleetRental.Application.Availability;
using FleetRental.Application.Bookings;
using FleetRental.Application.Cars;
using FleetRental.Application.Maintenance;
using FleetRental.Application.Notifications;
using FleetRental.Application.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace FleetRental.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the use-case services. They are scoped because each one holds the
    /// request's <see cref="Abstractions.IFleetRentalDbContext"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CarService>();
        services.AddScoped<BookingService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<AuthService>();
        services.AddScoped<BookingNotificationService>();
        services.AddScoped<Tenants.TenantService>();
        services.AddScoped<MaintenanceService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PlatformAdminService>();
        services.AddScoped<PlatformCompanyService>();
        services.AddScoped<PlatformCarService>();
        services.AddScoped<PlatformFeatureService>();
        services.AddScoped<TenantFeatureGate>();

        return services;
    }
}
