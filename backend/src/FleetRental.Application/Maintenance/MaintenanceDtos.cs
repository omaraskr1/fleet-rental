using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.Application.Maintenance;

public sealed record ServiceRecordDto
{
    public required Guid Id { get; init; }

    public required Guid CarId { get; init; }

    public required DateOnly PerformedAt { get; init; }

    public required string Description { get; init; }

    public int? OdometerKm { get; init; }

    public required decimal Cost { get; init; }

    public string? PerformedBy { get; init; }

    public Guid? ServiceTypeId { get; init; }

    public string? ServiceTypeName { get; init; }

    /// <summary>
    /// Requires ServiceType to be loaded when ServiceTypeId is set — callers pass
    /// it through eagerly (see MaintenanceService.LogServiceAsync) so this never
    /// silently lazy-loads.
    /// </summary>
    public static ServiceRecordDto FromEntity(ServiceRecord record) => new()
    {
        Id = record.Id,
        CarId = record.CarId,
        PerformedAt = record.PerformedAt,
        Description = record.Description,
        OdometerKm = record.OdometerKm,
        Cost = record.Cost,
        PerformedBy = record.PerformedBy,
        ServiceTypeId = record.ServiceTypeId,
        ServiceTypeName = record.ServiceType?.Name,
    };
}

public sealed record LogServiceRequest
{
    public required DateOnly PerformedAt { get; init; }

    public required string Description { get; init; }

    public int? OdometerKm { get; init; }

    public required decimal Cost { get; init; }

    public string? PerformedBy { get; init; }

    public Guid? ServiceTypeId { get; init; }
}

// ---------- Service catalog ----------

public sealed record ServiceTypeDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required int IntervalKm { get; init; }

    public required bool IsActive { get; init; }

    public static ServiceTypeDto FromEntity(ServiceType type) => new()
    {
        Id = type.Id,
        Name = type.Name,
        IntervalKm = type.IntervalKm,
        IsActive = type.IsActive,
    };
}

public sealed record CreateServiceTypeRequest
{
    public required string Name { get; init; }

    public required int IntervalKm { get; init; }
}

public sealed record UpdateServiceTypeRequest
{
    public required string Name { get; init; }

    public required int IntervalKm { get; init; }
}

/// <summary>
/// Km-until-due for one service type, on one car — the per-car breakdown the
/// "tab to each car" view needs, one row per active catalog entry.
/// </summary>
public sealed record ServiceTypeStatusDto
{
    public required Guid ServiceTypeId { get; init; }

    public required string ServiceTypeName { get; init; }

    public required int IntervalKm { get; init; }

    public DateOnly? LastPerformedAt { get; init; }

    /// <summary>Null when this service has never been logged for this car, or the car has no current odometer.</summary>
    public int? KmSinceLastService { get; init; }

    /// <summary>False whenever KmSinceLastService is null — silence is never "not due."</summary>
    public required bool IsDue { get; init; }
}

public sealed record VehicleIssueDto
{
    public required Guid Id { get; init; }

    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public required string ReportedByName { get; init; }

    public required string Description { get; init; }

    public required string Severity { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset ReportedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }

    public string? ResolutionNotes { get; init; }

    /// <summary>
    /// Requires Car and the reporting User to be loaded — callers use
    /// <c>MaintenanceQueries.WithDetails</c> so this never silently lazy-loads.
    /// </summary>
    public static VehicleIssueDto FromEntity(VehicleIssue issue, string reportedByName) => new()
    {
        Id = issue.Id,
        CarId = issue.CarId,
        CarName = issue.Car.Name,
        ReportedByName = reportedByName,
        Description = issue.Description,
        Severity = issue.Severity.ToString(),
        Status = issue.Status.ToString(),
        ReportedAt = issue.ReportedAt,
        ResolvedAt = issue.ResolvedAt,
        ResolutionNotes = issue.ResolutionNotes,
    };
}

public sealed record ReportIssueRequest
{
    public required string Description { get; init; }

    public required IssueSeverity Severity { get; init; }
}

public sealed record ResolveIssueRequest
{
    public string? ResolutionNotes { get; init; }
}

public sealed record UpdateOdometerRequest
{
    public required int Km { get; init; }
}

public sealed record SetServiceIntervalRequest
{
    public int? Km { get; init; }
}

/// <summary>
/// The at-a-glance state an owner needs for one car: is it due for service, and
/// does it have anything open that needs attention.
/// </summary>
public sealed record CarMaintenanceSummaryDto
{
    public required Guid CarId { get; init; }

    public required string CarName { get; init; }

    public int? CurrentOdometerKm { get; init; }

    public int? ServiceIntervalKm { get; init; }

    public DateOnly? LastServiceAt { get; init; }

    public int? KmSinceLastService { get; init; }

    /// <summary>
    /// False whenever the odometer or interval is not tracked — silence about a
    /// car's mileage should never be presented as "definitely not due."
    /// </summary>
    public required bool IsServiceDue { get; init; }

    public required int OpenIssueCount { get; init; }

    public required bool HasBlockingIssue { get; init; }
}
