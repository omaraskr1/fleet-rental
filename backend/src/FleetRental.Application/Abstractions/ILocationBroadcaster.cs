namespace FleetRental.Application.Abstractions;

/// <summary>
/// Pushes a freshly-ingested GPS reading to whoever is watching the map, live.
/// The interface lives here so <c>GpsService</c> stays free of a SignalR
/// dependency; the real implementation (wrapping <c>IHubContext&lt;LocationHub&gt;</c>)
/// lives in the Api project, the only layer that references ASP.NET Core's
/// realtime stack.
/// </summary>
public interface ILocationBroadcaster
{
    Task BroadcastAsync(Guid tenantId, CarLocationUpdate update, CancellationToken cancellationToken = default);
}

public sealed record CarLocationUpdate(
    Guid CarId,
    string CarName,
    double Latitude,
    double Longitude,
    DateTimeOffset RecordedAt);
