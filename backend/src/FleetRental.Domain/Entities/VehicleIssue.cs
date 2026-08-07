using FleetRental.Domain.Common;
using FleetRental.Domain.Enums;

namespace FleetRental.Domain.Entities;

/// <summary>
/// A mechanical or cosmetic problem reported against a car — the thing an
/// owner needs to see and act on before the vehicle goes out on its next booking.
/// </summary>
public class VehicleIssue : TenantEntity
{
    private VehicleIssue() { } // EF Core

    private VehicleIssue(Guid carId, Guid reportedByUserId, string description, IssueSeverity severity)
    {
        CarId = carId;
        ReportedByUserId = reportedByUserId;
        Description = description;
        Severity = severity;
        Status = IssueStatus.Open;
        ReportedAt = DateTimeOffset.UtcNow;
    }

    public Guid CarId { get; private set; }

    public Car Car { get; private set; } = null!;

    /// <summary>Always an admin — clients have no visibility into mechanical state.</summary>
    public Guid ReportedByUserId { get; private set; }

    public string Description { get; private set; } = null!;

    public IssueSeverity Severity { get; private set; }

    public IssueStatus Status { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>What was actually done to fix it — shown alongside the closed issue in history.</summary>
    public string? ResolutionNotes { get; private set; }

    /// <summary>A Critical, unresolved issue is the one condition that should stop a car going out.</summary>
    public bool BlocksBooking => Severity == IssueSeverity.Critical && Status != IssueStatus.Resolved;

    public static VehicleIssue Report(
        Guid carId,
        Guid reportedByUserId,
        string description,
        IssueSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Issue description is required.");
        }

        return new VehicleIssue(carId, reportedByUserId, description.Trim(), severity);
    }

    public void StartProgress()
    {
        if (Status != IssueStatus.Open)
        {
            throw new DomainException($"Only an open issue can move to in-progress; this one is {Status}.");
        }

        Status = IssueStatus.InProgress;
        Touch();
    }

    public void Resolve(string? resolutionNotes = null)
    {
        if (Status == IssueStatus.Resolved)
        {
            throw new DomainException("This issue is already resolved.");
        }

        Status = IssueStatus.Resolved;
        ResolvedAt = DateTimeOffset.UtcNow;
        ResolutionNotes = resolutionNotes?.Trim();
        Touch();
    }

    /// <summary>Reopens a resolved issue that turns out not to have been fixed.</summary>
    public void Reopen()
    {
        if (Status != IssueStatus.Resolved)
        {
            throw new DomainException($"Only a resolved issue can be reopened; this one is {Status}.");
        }

        Status = IssueStatus.Open;
        ResolvedAt = null;
        ResolutionNotes = null;
        Touch();
    }

    public void ChangeSeverity(IssueSeverity severity)
    {
        Severity = severity;
        Touch();
    }
}
