using FleetRental.Application.Common;
using FleetRental.Application.Platform;
using FleetRental.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FleetRental.Api.Filters;

/// <summary>
/// Gates an entire controller behind a per-company feature toggle —
/// <c>[RequireFeature(FeatureKey.Analytics)]</c> on <c>AnalyticsController</c>,
/// for example. This is the enforcement side; hiding the nav link in the
/// frontend is only ever a convenience on top of this, never a substitute — a
/// disabled feature must 403, not just be unlinked.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RequireFeatureAttribute(FeatureKey feature) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var gate = context.HttpContext.RequestServices.GetRequiredService<TenantFeatureGate>();

        if (!await gate.IsEnabledAsync(feature, context.HttpContext.RequestAborted))
        {
            throw new ForbiddenException($"The {feature} feature is not enabled for this company.");
        }

        await next();
    }
}
