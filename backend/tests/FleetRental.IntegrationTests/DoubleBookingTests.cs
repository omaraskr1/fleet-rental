using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetRental.IntegrationTests;

/// <summary>
/// The guarantee the whole product rests on: a car cannot be committed to two
/// clients for the same day.
/// </summary>
/// <remarks>
/// These run against real SQL Server precisely so the unique index is live. The
/// concurrency test below is the one that would catch a regression no amount of
/// single-threaded testing can find.
/// </remarks>
[Collection(nameof(ApiCollection))]
public class DoubleBookingTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(offset);

    public async Task InitializeAsync()
    {
        await factory.ResetAsync();
        await factory.SeedTenantAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Approving_an_overlapping_booking_is_rejected_with_409()
    {
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("a@test.com");
        var bookingA = await clientA.CreateBookingAndGetIdAsync(carId, Day(10), Day(14));

        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("b@test.com");
        var bookingB = await clientB.CreateBookingAndGetIdAsync(carId, Day(12), Day(16));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        var first = await admin.ApproveAsync(bookingA, "Confirmed");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await admin.ApproveAsync(bookingB);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<ApiClient.ProblemResult>(Json);
        Assert.Contains("booked by another request", problem!.Detail);

        // Only the first booking's days are held — the failed approval left nothing behind.
        Assert.Equal(5, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Two_simultaneous_approvals_for_the_same_dates_produce_exactly_one_winner()
    {
        // The race the unique index exists for. Both requests read availability,
        // both see the range as free, and both try to claim it at once. Without a
        // database-level constraint, both would commit.
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("race-a@test.com");
        var bookingA = await clientA.CreateBookingAndGetIdAsync(carId, Day(20), Day(24));

        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("race-b@test.com");
        var bookingB = await clientB.CreateBookingAndGetIdAsync(carId, Day(22), Day(26));

        // Two separate admin sessions, mimicking two people clicking at once.
        var adminOne = factory.CreateTenantClient();
        adminOne.Authenticate(await adminOne.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        var adminTwo = factory.CreateTenantClient();
        adminTwo.Authenticate(await adminTwo.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        var results = await Task.WhenAll(
            adminOne.ApproveAsync(bookingA),
            adminTwo.ApproveAsync(bookingB));

        var succeeded = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflicted = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.True(succeeded == 1,
            $"expected exactly 1 approval to win, got {succeeded} " +
            $"(statuses: {string.Join(", ", results.Select(r => (int)r.StatusCode))})");
        Assert.Equal(1, conflicted);

        // Whichever won, exactly one 5-day booking is held — never 10, never
        // an overlapping 9.
        Assert.Equal(5, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Adjacent_bookings_are_both_approved()
    {
        // 10-14 and 15-18 do not overlap. If the boundary were wrong, the fleet
        // would silently lose a bookable day between every rental.
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("adj-a@test.com");
        var bookingA = await clientA.CreateBookingAndGetIdAsync(carId, Day(10), Day(14));

        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("adj-b@test.com");
        var bookingB = await clientB.CreateBookingAndGetIdAsync(carId, Day(15), Day(18));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        Assert.Equal(HttpStatusCode.OK, (await admin.ApproveAsync(bookingA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.ApproveAsync(bookingB)).StatusCode);

        Assert.Equal(9, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Cancelling_an_approved_booking_frees_the_dates_for_someone_else()
    {
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("cancel-a@test.com");
        var bookingA = await clientA.CreateBookingAndGetIdAsync(carId, Day(30), Day(34));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));
        await admin.ApproveAsync(bookingA);
        Assert.Equal(5, await factory.CountBookedDaysAsync(carId));

        await clientA.CancelAsync(bookingA);
        Assert.Equal(0, await factory.CountBookedDaysAsync(carId));

        // The dates are genuinely reusable, not just marked cancelled.
        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("cancel-b@test.com");
        var bookingB = await clientB.CreateBookingAndGetIdAsync(carId, Day(30), Day(34));

        Assert.Equal(HttpStatusCode.OK, (await admin.ApproveAsync(bookingB)).StatusCode);
        Assert.Equal(5, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Rejecting_a_booking_holds_no_dates()
    {
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("rej@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, Day(40), Day(44));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        Assert.Equal(HttpStatusCode.OK, (await admin.RejectAsync(bookingId, "Committed")).StatusCode);
        Assert.Equal(0, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Pending_requests_may_overlap_freely()
    {
        // Competition before a decision is intentional: several clients can want
        // the same dates, and the admin picks.
        var carId = await factory.SeedCarAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("pend-a@test.com");
        Assert.Equal(HttpStatusCode.Created,
            (await clientA.CreateBookingAsync(carId, Day(50), Day(54))).StatusCode);

        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("pend-b@test.com");
        Assert.Equal(HttpStatusCode.Created,
            (await clientB.CreateBookingAsync(carId, Day(50), Day(54))).StatusCode);

        Assert.Equal(0, await factory.CountBookedDaysAsync(carId));
    }

    [Fact]
    public async Task Requesting_dates_already_held_is_refused_at_submission()
    {
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var clientA = factory.CreateTenantClient();
        await clientA.SignUpAndAuthenticateAsync("held-a@test.com");
        var bookingA = await clientA.CreateBookingAndGetIdAsync(carId, Day(60), Day(64));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));
        await admin.ApproveAsync(bookingA);

        // Better to refuse now with a clear message than let the client fill in
        // event details for a range that cannot be granted.
        var clientB = factory.CreateTenantClient();
        await clientB.SignUpAndAuthenticateAsync("held-b@test.com");
        var response = await clientB.CreateBookingAsync(carId, Day(62), Day(66));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Availability_reflects_exactly_the_approved_days()
    {
        var carId = await factory.SeedCarAsync();
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync();

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("avail@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(carId, Day(70), Day(72));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));
        await admin.ApproveAsync(bookingId);

        var viewer = factory.CreateTenantClient();
        await viewer.SignUpAndAuthenticateAsync("avail-viewer@test.com");
        var availability = await viewer.GetAsync<ApiClient.AvailabilityResult>(
            $"/api/cars/{carId}/availability?from={Day(65):yyyy-MM-dd}&to={Day(80):yyyy-MM-dd}");

        Assert.Equal(3, availability!.BookedDates.Length);
        Assert.Equal(
            [Day(70).ToString("yyyy-MM-dd"), Day(71).ToString("yyyy-MM-dd"), Day(72).ToString("yyyy-MM-dd")],
            availability.BookedDates.Order().ToArray());
    }
}
