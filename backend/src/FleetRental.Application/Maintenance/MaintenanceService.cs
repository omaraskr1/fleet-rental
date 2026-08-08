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

        if (request.ServiceTypeId is { } serviceTypeId)
        {
            // Loaded (not AsNoTracking) so it stays in the change tracker: EF's
            // relationship fixup wires record.ServiceType from this FK match
            // once the record below is added, with no second query needed.
            _ = await db.ServiceTypes.FirstOrDefaultAsync(t => t.Id == serviceTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ServiceType), serviceTypeId);
        }

        var record = ServiceRecord.Log(
            carId,
            request.PerformedAt,
            request.Description,
            request.OdometerKm,
            request.Cost,
            request.PerformedBy,
            request.ServiceTypeId);

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
            .Include(s => s.ServiceType)
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

    // ---------- Service catalog ----------

    /// <summary>Defined once per tenant, shared across every car — not scoped to one.</summary>
    public async Task<IReadOnlyList<ServiceTypeDto>> GetServiceTypesAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var types = await db.ServiceTypes
            .AsNoTracking()
            .Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return [.. types.Select(ServiceTypeDto.FromEntity)];
    }

    public async Task<ServiceTypeDto> CreateServiceTypeAsync(
        CreateServiceTypeRequest request, CancellationToken cancellationToken = default)
    {
        var type = ServiceType.Create(request.Name, request.IntervalKm);
        db.ServiceTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceTypeDto.FromEntity(type);
    }

    public Task<ServiceTypeDto> UpdateServiceTypeAsync(
        Guid serviceTypeId, UpdateServiceTypeRequest request, CancellationToken cancellationToken = default) =>
        MutateServiceTypeAsync(serviceTypeId, type => type.UpdateDetails(request.Name, request.IntervalKm), cancellationToken);

    public Task<ServiceTypeDto> DeactivateServiceTypeAsync(Guid serviceTypeId, CancellationToken cancellationToken = default) =>
        MutateServiceTypeAsync(serviceTypeId, type => type.Deactivate(), cancellationToken);

    public Task<ServiceTypeDto> ReactivateServiceTypeAsync(Guid serviceTypeId, CancellationToken cancellationToken = default) =>
        MutateServiceTypeAsync(serviceTypeId, type => type.Reactivate(), cancellationToken);

    private async Task<ServiceTypeDto> MutateServiceTypeAsync(
        Guid serviceTypeId, Action<ServiceType> mutate, CancellationToken cancellationToken)
    {
        var type = await db.ServiceTypes.FirstOrDefaultAsync(t => t.Id == serviceTypeId, cancellationToken)
            ?? throw new NotFoundException(nameof(ServiceType), serviceTypeId);

        mutate(type);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceTypeDto.FromEntity(type);
    }

    /// <summary>
    /// Km-until-due for every active service type, on one car — the "tab to each
    /// car" breakdown. A type never performed on this car still appears, with a
    /// null KmSinceLastService rather than being omitted, matching the same
    /// "silence is not the same as up to date" rule GetSummaryAsync uses for the
    /// generic interval.
    /// </summary>
    public async Task<IReadOnlyList<ServiceTypeStatusDto>> GetServiceTypeStatusesAsync(
        Guid carId, CancellationToken cancellationToken = default)
    {
        var car = await db.Cars.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carId, cancellationToken)
            ?? throw new NotFoundException(nameof(Car), carId);

        var types = await db.ServiceTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        if (types.Count == 0)
        {
            return [];
        }

        var typeIds = types.Select(t => t.Id).ToList();

        // Most recent odometer-bearing record per service type for this car —
        // same "must carry a reading" rule GetSummaryAsync applies to the
        // generic interval, since a record without one cannot anchor the math.
        var lastByType = await db.ServiceRecords
            .AsNoTracking()
            .Where(s => s.CarId == carId && s.ServiceTypeId != null && typeIds.Contains(s.ServiceTypeId!.Value)
                && s.OdometerKm != null)
            .GroupBy(s => s.ServiceTypeId!.Value)
            .Select(g => new
            {
                ServiceTypeId = g.Key,
                Last = g.OrderByDescending(s => s.PerformedAt).First(),
            })
            .ToDictionaryAsync(x => x.ServiceTypeId, x => x.Last, cancellationToken);

        return [.. types.Select(type =>
        {
            var last = lastByType.GetValueOrDefault(type.Id);
            var kmSinceLastService = last is not null && car.CurrentOdometerKm is { } current
                ? current - last.OdometerKm!.Value
                : (int?)null;

            return new ServiceTypeStatusDto
            {
                ServiceTypeId = type.Id,
                ServiceTypeName = type.Name,
                IntervalKm = type.IntervalKm,
                LastPerformedAt = last?.PerformedAt,
                KmSinceLastService = kmSinceLastService,
                IsDue = kmSinceLastService is { } km && km >= type.IntervalKm,
            };
        })];
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
