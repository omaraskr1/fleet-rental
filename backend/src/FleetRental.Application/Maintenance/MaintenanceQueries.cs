using FleetRental.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Maintenance;

internal static class MaintenanceQueries
{
    /// <summary>Loads the Car navigation VehicleIssueDto.FromEntity reads.</summary>
    public static IQueryable<VehicleIssue> WithDetails(this IQueryable<VehicleIssue> query) =>
        query.Include(i => i.Car);
}
