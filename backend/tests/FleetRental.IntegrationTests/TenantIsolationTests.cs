using System.Net;
using System.Net.Http.Json;
using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Proves tenants cannot see each other. With a shared database this is the
/// property the entire product model depends on: if it fails, one paying client
/// can read another's fleet, customers and revenue.
/// </summary>
[Collection(nameof(ApiCollection))]
public class TenantIsolationTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    private static DateOnly Day(int offset) => DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(offset);

    public Task InitializeAsync() => factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Every_persisted_entity_except_Tenant_and_PlatformAdmin_is_tenant_owned()
    {
        // A new entity that forgets to derive from TenantEntity gets no query
        // filter and silently becomes globally visible. Rather than rely on
        // remembering, this fails the build's test run the moment it happens.
        //
        // PlatformAdmin is excluded alongside Tenant for the same reason: it is
        // the thing operating across tenants, not something owned by one.
        var entityTypes = typeof(Car).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true })
            .Where(t => typeof(Entity).IsAssignableFrom(t))
            .Where(t => t != typeof(Tenant) && t != typeof(PlatformAdmin))
            .ToList();

        Assert.NotEmpty(entityTypes);

        var unowned = entityTypes
            .Where(t => !typeof(ITenantOwned).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(unowned.Count == 0,
            $"These entities are not tenant-owned and would leak across tenants: {string.Join(", ", unowned)}");
    }

    [Fact]
    public async Task A_tenants_cars_are_invisible_to_another_tenant()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        await factory.SeedCarAsync(alpha, "Alpha Van");
        await factory.SeedCarAsync(alpha, "Alpha Sedan");
        await factory.SeedCarAsync(beta, "Beta Truck");

        var alphaClient = factory.CreateTenantClient("alpha-rentals");
        await alphaClient.SignUpAndAuthenticateAsync("a-viewer@alpha.com");
        var betaClient = factory.CreateTenantClient("beta-motors");
        await betaClient.SignUpAndAuthenticateAsync("b-viewer@beta.com");

        var alphaCars = await alphaClient.GetAsync<CarSummary[]>("/api/cars");
        var betaCars = await betaClient.GetAsync<CarSummary[]>("/api/cars");

        Assert.Equal(2, alphaCars!.Length);
        Assert.Single(betaCars!);
        Assert.All(alphaCars, c => Assert.StartsWith("Alpha", c.Name));
        Assert.Equal("Beta Truck", betaCars![0].Name);
    }

    [Fact]
    public async Task An_authenticated_caller_cannot_switch_tenants_with_a_header()
    {
        // The attack the precedence rule exists to stop: sign in legitimately at
        // one company, then set X-Tenant-Code to another and read their fleet.
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        await factory.SeedCarAsync(alpha, "Alpha Van");
        await factory.SeedCarAsync(beta, "Beta Truck");

        var client = factory.CreateTenantClient("alpha-rentals");
        await client.SignUpAndAuthenticateAsync("spy@alpha.com");

        // Same authenticated session, now claiming to be beta.
        client.Http.DefaultRequestHeaders.Remove("X-Tenant-Code");
        client.Http.DefaultRequestHeaders.Add("X-Tenant-Code", "beta-motors");

        var cars = await client.GetAsync<CarSummary[]>("/api/cars");

        // The signed claim wins; the header is ignored entirely.
        Assert.Single(cars!);
        Assert.Equal("Alpha Van", cars![0].Name);
    }

    [Fact]
    public async Task The_same_email_can_hold_an_account_at_two_companies()
    {
        // Requires the unique index to be (TenantId, Email) rather than global.
        // A platform-wide constraint would make the second signup impossible.
        await factory.SeedTenantAsync("alpha-rentals");
        await factory.SeedTenantAsync("beta-motors");

        var atAlpha = factory.CreateTenantClient("alpha-rentals");
        var atBeta = factory.CreateTenantClient("beta-motors");

        var first = await atAlpha.Http.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "amira@example.com",
            password = "SharedPass123",
            fullName = "Amira Hassan",
        });

        var second = await atBeta.Http.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "amira@example.com",
            password = "DifferentPass123",
            fullName = "Amira Hassan",
        });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Signing_up_twice_at_the_same_company_is_still_refused()
    {
        await factory.SeedTenantAsync("alpha-rentals");
        var client = factory.CreateTenantClient("alpha-rentals");

        await client.SignUpAsync("dupe@alpha.com");

        var second = await client.Http.PostAsJsonAsync("/api/auth/signup", new
        {
            email = "dupe@alpha.com",
            password = "AnotherPass123",
            fullName = "Impostor",
        });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Credentials_from_one_company_do_not_work_at_another()
    {
        await factory.SeedTenantAsync("alpha-rentals");
        await factory.SeedTenantAsync("beta-motors");

        var atAlpha = factory.CreateTenantClient("alpha-rentals");
        await atAlpha.SignUpAsync("member@alpha.com");

        var atBeta = factory.CreateTenantClient("beta-motors");
        var response = await atBeta.Http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "member@alpha.com",
            password = "ClientPass123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_read_another_tenants_booking_queue()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");
        var betaClient = factory.CreateTenantClient("beta-motors");
        await betaClient.SignUpAndAuthenticateAsync("client@beta.com");
        await betaClient.CreateBookingAsync(betaCar, Day(10), Day(12));

        var (email, password) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(email, password, asAdmin: true));

        var queue = await alphaAdmin.GetAsync<ApiClient.BookingResult[]>("/api/bookings");

        // Beta has a pending booking; alpha's admin must not see it.
        Assert.Empty(queue!);
    }

    [Fact]
    public async Task An_admin_cannot_approve_another_tenants_booking_by_id()
    {
        // Even holding the exact booking id, the query filter means it does not
        // exist from the other tenant's perspective — a 404, not a 403, because
        // confirming it exists would already leak something.
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");
        var betaClient = factory.CreateTenantClient("beta-motors");
        await betaClient.SignUpAndAuthenticateAsync("client@beta.com");
        var bookingId = await betaClient.CreateBookingAndGetIdAsync(betaCar, Day(10), Day(12));

        var (email, password) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(email, password, asAdmin: true));

        var response = await alphaAdmin.ApproveAsync(bookingId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.CountBookedDaysAsync(betaCar));
    }

    [Fact]
    public async Task A_cars_availability_is_not_readable_from_another_tenant()
    {
        var beta = await factory.SeedTenantAsync("beta-motors");
        await factory.SeedTenantAsync("alpha-rentals");
        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");

        var alphaClient = factory.CreateTenantClient("alpha-rentals");
        await alphaClient.SignUpAndAuthenticateAsync("alpha-browser@test.com");
        var response = await alphaClient.Http.GetAsync($"/api/cars/{betaCar}/availability");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Two_tenants_can_hold_the_same_date_on_their_own_cars()
    {
        // Isolation must not over-reach: the double-booking constraint applies per
        // car, so two businesses booking the same calendar day is entirely normal.
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var beta = await factory.SeedTenantAsync("beta-motors");

        var alphaCar = await factory.SeedCarAsync(alpha, "Alpha Van");
        var betaCar = await factory.SeedCarAsync(beta, "Beta Truck");

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "admin@alpha.com");
        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "admin@beta.com");

        var alphaClient = factory.CreateTenantClient("alpha-rentals");
        await alphaClient.SignUpAndAuthenticateAsync("c@alpha.com");
        var alphaBooking = await alphaClient.CreateBookingAndGetIdAsync(alphaCar, Day(10), Day(14));

        var betaClient = factory.CreateTenantClient("beta-motors");
        await betaClient.SignUpAndAuthenticateAsync("c@beta.com");
        var betaBooking = await betaClient.CreateBookingAndGetIdAsync(betaCar, Day(10), Day(14));

        var alphaAdmin = factory.CreateTenantClient("alpha-rentals");
        alphaAdmin.Authenticate(await alphaAdmin.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var betaAdmin = factory.CreateTenantClient("beta-motors");
        betaAdmin.Authenticate(await betaAdmin.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        Assert.Equal(HttpStatusCode.OK, (await alphaAdmin.ApproveAsync(alphaBooking)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await betaAdmin.ApproveAsync(betaBooking)).StatusCode);

        Assert.Equal(5, await factory.CountBookedDaysAsync(alphaCar));
        Assert.Equal(5, await factory.CountBookedDaysAsync(betaCar));
    }

    [Fact]
    public async Task An_unknown_company_code_yields_nothing_rather_than_everything()
    {
        // The filters fail closed. If they failed open, a typo in the company code
        // would expose every tenant's fleet at once. GET /api/cars now requires
        // authentication outright (see Browsing_cars_requires_an_account in
        // AuthorizationTests), so the still-anonymous surface that exercises tenant
        // resolution is login: real credentials from a real tenant must not work
        // under a bogus company code.
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var atAlpha = factory.CreateTenantClient("alpha-rentals");
        await atAlpha.SignUpAsync("real-user@alpha.com");

        var stranger = factory.CreateTenantClient("no-such-company");
        var response = await stranger.Http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "real-user@alpha.com",
            password = "ClientPass123",
        });

        // No tenant resolves at all for a bogus code, so this fails before
        // credential-checking ever runs — a 400 (bad request), not a 401.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_request_with_no_company_code_sees_nothing()
    {
        var alpha = await factory.SeedTenantAsync("alpha-rentals");
        var atAlpha = factory.CreateTenantClient("alpha-rentals");
        await atAlpha.SignUpAsync("real-user-2@alpha.com");

        var anonymous = new ApiClient(factory.CreateClient());
        var response = await anonymous.Http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "real-user-2@alpha.com",
            password = "ClientPass123",
        });

        // Same as above: no header means no tenant, which fails closed at 400.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_company_code_endpoint_reveals_only_a_display_name()
    {
        await factory.SeedTenantAsync("alpha-rentals", "Alpha Rentals LLC");

        var response = await factory.CreateClient().GetAsync("/api/tenants/ALPHA-RENTALS");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TenantSummary>();

        // Case-insensitive lookup, and nothing beyond name and code comes back.
        Assert.Equal("Alpha Rentals LLC", body!.Name);
        Assert.Equal("alpha-rentals", body.Code);
    }

    [Fact]
    public async Task An_unknown_company_code_returns_404_without_confirming_existence()
    {
        var response = await factory.CreateClient().GetAsync("/api/tenants/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_suspended_tenant_is_treated_as_unknown()
    {
        var suspended = await factory.SeedTenantAsync("lapsed-fleet");
        await factory.SeedCarAsync(suspended, "Lapsed Van");

        var client = factory.CreateTenantClient("lapsed-fleet");
        await client.SignUpAsync("was-a-member@lapsed.com");

        await factory.SuspendTenantAsync(suspended);

        var lookup = await factory.CreateClient().GetAsync("/api/tenants/lapsed-fleet");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);

        // And a login that worked before suspension stops resolving too — same
        // "no tenant" 400 as an unknown company code, since a suspended tenant
        // resolves to nothing at all.
        var loginResponse = await factory.CreateTenantClient("lapsed-fleet").Http.PostAsJsonAsync("/api/auth/login", new
        {
            email = "was-a-member@lapsed.com",
            password = "ClientPass123",
        });
        Assert.Equal(HttpStatusCode.BadRequest, loginResponse.StatusCode);
    }

    private sealed record CarSummary(Guid Id, string Name);

    private sealed record TenantSummary(string Code, string Name);
}
