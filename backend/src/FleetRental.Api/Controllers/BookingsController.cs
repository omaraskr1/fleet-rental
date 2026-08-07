using FleetRental.Api.Extensions;
using FleetRental.Application.Availability;
using FleetRental.Application.Bookings;
using FleetRental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetRental.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(BookingService bookings, AvailabilityService availability) : ControllerBase
{
    /// <summary>Submits a booking request (feature 3).</summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequest request, CancellationToken ct)
    {
        // Client id comes from the token, never from the request body.
        var booking = await bookings.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    /// <summary>The signed-in client's own requests.</summary>
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine(
        [FromQuery] BookingStatus? status,
        CancellationToken ct) =>
        Ok(await bookings.GetForClientAsync(User.GetUserId(), status, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BookingDto>> GetById(Guid id, CancellationToken ct)
    {
        var booking = await bookings.GetByIdAsync(id, ct);

        // Without this a client could read anyone's booking, including the
        // requester's name and email, by guessing an id.
        if (!User.IsAdmin() && booking.ClientId != User.GetUserId())
        {
            return Forbid();
        }

        return Ok(booking);
    }

    /// <summary>Client withdraws their request, releasing any held dates.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await bookings.CancelAsync(id, User.GetUserId(), User.IsAdmin(), ct));

    // ---------- Admin panel (feature 4) ----------

    /// <summary>Every request across the fleet, pending first.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<IReadOnlyList<BookingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetAll(
        [FromQuery] BookingStatus? status,
        [FromQuery] Guid? carId,
        CancellationToken ct) =>
        Ok(await bookings.GetAllAsync(status, carId, ct));

    /// <summary>
    /// Approves a request. Returns 409 if the dates were claimed in the meantime —
    /// the client should refresh the queue rather than retry blindly.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Approve(Guid id, DecideBookingRequest request, CancellationToken ct) =>
        Ok(await bookings.ApproveAsync(id, User.GetUserId(), request.Reason, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingDto>> Reject(Guid id, DecideBookingRequest request, CancellationToken ct) =>
        Ok(await bookings.RejectAsync(id, User.GetUserId(), request.Reason, ct));

    /// <summary>Fleet-wide calendar across all cars (feature 4).</summary>
    [HttpGet("/api/fleet/availability")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType<FleetAvailabilityDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FleetAvailabilityDto>> GetFleetAvailability(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool includeRetired = false,
        CancellationToken ct = default) =>
        Ok(await availability.GetFleetAvailabilityAsync(from, to, includeRetired, ct));
}
