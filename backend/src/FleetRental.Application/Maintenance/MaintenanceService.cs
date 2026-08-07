using FleetRental.Application.Abstractions;
using FleetRental.Application.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FleetRental.Application.Maintenance;

/// <summary>
/// Mechanical history and issue tracking for the fleet — what an owner needs to
/// answer "what has been done to this car" and "does anything need attention
/// before it goes out again."
/// </summary>
public class MaintenanceService(IFleetRentalDbContext db)
{
    // ---------- Service history ----------

    public async Task<ServiceRecordDto> LogServiceAsync(
        Guid carId,
        LogServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        var record = ServiceRecord.Log(
            carId,
            request.PerformedAt,
            request.Description,
            request.OdometerKm,
            request.Cost,
            request.PerformedBy);

        db.ServiceRecords.Add(record);

        // A service visit is the natural moment an odometer reading is taken, so
        // logging one keeps the car's current reading current too — without this
        // an admin would have to remember to update it separately every time.
        if (request.OdometerKm is { } km && (car.CurrentOdometerKm is null || km > car.CurrentOdometerKm))
        {
            car.UpdateOdometer(km);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ServiceRecordDto.FromEntity(record);
    }

    public async Task<IReadOnlyList<ServiceRecordDto>> GetServiceHistoryAsync(
        Guid carId,
        CancellationToken cancellationToken = default)
    {
        var records = await db.ServiceRecords
            .AsNoTracking()
            .Where(s => s.CarId == carId)
            .OrderByDescending(s => s.PerformedAt)
            .ToListAsync(cancellationToken);

        return [.. records.Select(ServiceRecordDto.FromEntity)];
    }

    public async Task UpdateOdometerAsync(Guid carId, int km, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.UpdateOdometer(km);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetServiceIntervalAsync(Guid carId, int? km, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        car.SetServiceInterval(km);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Issues ----------

    public async Task<VehicleIssueDto> ReportIssueAsync(
        Guid carId,
        Guid reportedByUserId,
        ReportIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        var reporter = await db.Users.FirstOrDefaultAsync(u => u.Id == reportedByUserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), reportedByUserId);

        var issue = VehicleIssue.Report(carId, reportedByUserId, request.Description, request.Severity);
        db.VehicleIssues.Add(issue);

        // No AsNoTracking above: car and issue are both tracked in this context,
        // so EF's relationship fixup wires issue.Car to the loaded car instance
        // via the FK match — no second query needed for FromEntity to read the name.
        await db.SaveChangesAsync(cancellationToken);

        return VehicleIssueDto.FromEntity(issue, reporter.FullName);
    }

    /// <summary>Issues for one car, or across the whole fleet when <paramref name="carId"/> is null.</summary>
    public async Task<IReadOnlyList<VehicleIssueDto>> GetIssuesAsync(
        Guid? carId = null,
        IssueStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var issues = await db.VehicleIssues
            .AsNoTracking()
            .WithDetails()
            .Where(i => carId == null || i.CarId == carId)
            .Where(i => status == null || i.Status == status)
            .OrderByDescending(i => i.Severity)
            .ThenByDescending(i => i.ReportedAt)
            .ToListAsync(cancellationToken);

        var reporterIds = issues.Select(i => i.ReportedByUserId).Distinct().ToList();
        var reporters = await db.Users
            .AsNoTracking()
            .Where(u => reporterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return [.. issues.Select(i => VehicleIssueDto.FromEntity(i, reporters.GetValueOrDefault(i.ReportedByUserId, "—")))];
    }

    public Task<VehicleIssueDto> StartProgressAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        MutateIssueAsync(issueId, issue => issue.StartProgress(), cancellationToken);

    public Task<VehicleIssueDto> ResolveIssueAsync(
        Guid issueId,
        ResolveIssueRequest request,
        CancellationToken cancellationToken = default) =>
        MutateIssueAsync(issueId, issue => issue.Resolve(request.ResolutionNotes), cancellationToken);

    public Task<VehicleIssueDto> ReopenIssueAsync(Guid issueId, CancellationToken cancellationToken = default) =>
        MutateIssueAsync(issueId, issue => issue.Reopen(), cancellationToken);

    // ---------- Summary ----------

    public async Task<CarMaintenanceSummaryDto> GetSummaryAsync(Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        // The most recent record that actually carries an odometer reading —
        // not simply the most recent record — is what the due-date math anchors
        // on, since an entry like "windscreen chip repair" has none.
        var lastServiceWithOdometer = await db.ServiceRecords
            .AsNoTracking()
            .Where(s => s.CarId == carId && s.OdometerKm != null)
            .OrderByDescending(s => s.PerformedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastServiceAt = await db.ServiceRecords
            .AsNoTracking()
            .Where(s => s.CarId == carId)
            .OrderByDescending(s => s.PerformedAt)
            .Select(s => (DateOnly?)s.PerformedAt)
            .FirstOrDefaultAsync(cancellationToken);

        int? kmSinceLastService = car.CurrentOdometerKm is { } current
            ? current - (lastServiceWithOdometer?.OdometerKm ?? 0)
            : null;

        var isDue = car.ServiceIntervalKm is { } interval && kmSinceLastService is { } km && km >= interval;

        var openIssues = await db.VehicleIssues
            .AsNoTracking()
            .Where(i => i.CarId == carId && i.Status != IssueStatus.Resolved)
            .ToListAsync(cancellationToken);

        return new CarMaintenanceSummaryDto
        {
            CarId = car.Id,
            CarName = car.Name,
            CurrentOdometerKm = car.CurrentOdometerKm,
            ServiceIntervalKm = car.ServiceIntervalKm,
            LastServiceAt = lastServiceAt,
            KmSinceLastService = kmSinceLastService,
            IsServiceDue = isDue,
            OpenIssueCount = openIssues.Count,
            HasBlockingIssue = openIssues.Any(i => i.Severity == IssueSeverity.Critical),
        };
    }

    private async Task<VehicleIssueDto> MutateIssueAsync(
        Guid issueId,
        Action<VehicleIssue> mutate,
        CancellationToken cancellationToken)
    {
        var issue = await db.VehicleIssues.WithDetails()
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleIssue), issueId);

        mutate(issue);
        await db.SaveChangesAsync(cancellationToken);

        var reporter = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == issue.ReportedByUserId, cancellationToken);

        return VehicleIssueDto.FromEntity(issue, reporter?.FullName ?? "—");
    }
}
