using FleetRental.Api.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetRental.Api.Realtime;

/// <summary>
/// Pushes live GPS readings to fleet owners watching the map. The first use of
/// SignalR in this codebase — everything else is plain REST.
/// </summary>
/// <remarks>
/// A hub connection is long-lived, unlike a request, so tenant scoping can't go
/// through <c>ITenantContext</c>/<c>TenantResolutionMiddleware</c> (both are
/// per-request). Instead <see cref="OnConnectedAsync"/> reads the same signed
/// <c>tenant_id</c> claim the middleware trusts directly off the connection's
/// principal, once, and joins a per-tenant group — the isolation boundary for
/// broadcasts is "which group a message goes to," not a query filter.
/// </remarks>
[Authorize(Roles = nameof(Domain.Enums.UserRole.Admin))]
public class LocationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst(TenantResolutionMiddleware.TenantClaimType)?.Value;

        if (!string.IsNullOrEmpty(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId));
        }

        await base.OnConnectedAsync();
    }

    public static string GroupName(string tenantId) => $"tenant-{tenantId}";
}
