using FleetRental.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace FleetRental.Api.Realtime;

public class SignalRLocationBroadcaster(IHubContext<LocationHub> hub) : ILocationBroadcaster
{
    public Task BroadcastAsync(Guid tenantId, CarLocationUpdate update, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(LocationHub.GroupName(tenantId.ToString())).SendAsync("locationUpdated", update, cancellationToken);
}
