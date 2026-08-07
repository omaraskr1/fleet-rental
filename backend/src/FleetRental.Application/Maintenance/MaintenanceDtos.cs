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

    public static ServiceRecordDto FromEntity(ServiceRecord record) => new()
    {
        Id = record.Id,
        CarId = record.CarId,
        PerformedAt = record.PerformedAt,
        Description = record.Description,
        OdometerKm = record.OdometerKm,
        Cost = record.Cost,
        PerformedBy = record.PerformedBy,
    };
}

public sealed record LogServiceRequest
{
    public required DateOnly PerformedAt { get; init; }

    public required string Description { get; init; }

    public int? OdometerKm { get; init; }

    public required decimal Cost { get; init; }

    public string? PerformedBy { get; init; }
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
