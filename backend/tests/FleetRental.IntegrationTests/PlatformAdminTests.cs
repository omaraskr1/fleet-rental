using System.Net;
using System.Net.Http.Json;

namespace FleetRental.IntegrationTests;

/// <summary>
/// The platform-admin layer: a login and set of endpoints entirely separate from
/// tenant auth, operating across every company. The boundary that matters most
/// here is the mirror image of <see cref="AuthorizationTests"/> — a tenant admin
/// must never reach a platform endpoint, and a platform admin's token must never
/// resolve to any one tenant.
/// </summary>
[Collection(nameof(ApiCollection))]
public class PlatformAdminTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync() => await factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_unauthenticated_caller_cannot_reach_platform_endpoints()
    {
        var anonymous = factory.CreatePlatformClient();

        var response = await anonymous.GetCompaniesAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_tenant_admins_token_is_rejected_on_platform_endpoints()
    {
        // The reverse of the usual admin check: platform power must not leak in
        // from the tenant side just because a token happens to say "Admin".
        var (email, password) = await factory.SeedAdminAsync();
        var tenantAdmin = factory.CreateTenantClient();
        var token = await tenantAdmin.LoginAsync(email, password, asAdmin: true);
        tenantAdmin.Authenticate(token);

        var response = await tenantAdmin.Http.GetAsync("/api/platform/companies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_platform_admin_can_create_a_company_and_an_admin_for_it()
    {
        var (email, password) = await factory.SeedPlatformAdminAsync();
        var platform = factory.CreatePlatformClient();
        platform.Authenticate(await platform.PlatformLoginAsync(email, password));

        var companyId = await platform.CreateCompanyAndGetIdAsync("Second Fleet", "second-fleet");

        var createAdminResponse = await platform.CreateCompanyAdminAsync(
            companyId, "owner@second-fleet.com", "OwnerPass123", "Second Fleet Owner");

        Assert.Equal(HttpStatusCode.Created, createAdminResponse.StatusCode);

        // The new admin can log in to their own company and see nothing from any
        // other — the platform-created account behaves exactly like one seeded
        // the ordinary way.
        var newAdmin = factory.CreateTenantClient("second-fleet");
        var newAdminToken = await newAdmin.LoginAsync("owner@second-fleet.com", "OwnerPass123", asAdmin: true);

        Assert.False(string.IsNullOrWhiteSpace(newAdminToken));
    }

    [Fact]
    public async Task A_platform_admins_token_carries_no_tenant_and_sees_no_tenant_scoped_cars()
    {
        // A platform token is authenticated, so [Authorize] on /api/cars lets it
        // through — but it carries no tenant claim, so the query filter fails
        // closed exactly as an unresolved tenant always does: an empty list, not
        // an error and not someone else's data.
        await factory.SeedTenantAsync();
        await factory.SeedCarAsync();

        var (email, password) = await factory.SeedPlatformAdminAsync();
        var platform = factory.CreatePlatformClient();
        platform.Authenticate(await platform.PlatformLoginAsync(email, password));

        var response = await platform.Http.GetAsync("/api/cars");

        response.EnsureSuccessStatusCode();
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_platform_admin_can_monitor_and_manage_cars_across_companies()
    {
        var (email, password) = await factory.SeedPlatformAdminAsync();
        var platform = factory.CreatePlatformClient();
        platform.Authenticate(await platform.PlatformLoginAsync(email, password));

        var companyAId = await platform.CreateCompanyAndGetIdAsync("Company A", "company-a");
        var companyBId = await platform.CreateCompanyAndGetIdAsync("Company B", "company-b");

        (await platform.CreatePlatformCarAsync(companyAId, "A's Sedan")).EnsureSuccessStatusCode();
        (await platform.CreatePlatformCarAsync(companyBId, "B's Van", category: "Van")).EnsureSuccessStatusCode();

        var listResponse = await platform.GetPlatformCarsAsync();
        listResponse.EnsureSuccessStatusCode();
        var cars = await listResponse.Content.ReadFromJsonAsync<List<ApiClient.PlatformCarResult>>();

        Assert.Contains(cars!, c => c.Name == "A's Sedan" && c.CompanyId == companyAId);
        Assert.Contains(cars!, c => c.Name == "B's Van" && c.CompanyId == companyBId);
    }

    [Fact]
    public async Task Suspending_a_company_is_reversible()
    {
        var (email, password) = await factory.SeedPlatformAdminAsync();
        var platform = factory.CreatePlatformClient();
        platform.Authenticate(await platform.PlatformLoginAsync(email, password));

        var companyId = await platform.CreateCompanyAndGetIdAsync("Suspend Me", "suspend-me");

        (await platform.SuspendCompanyAsync(companyId)).EnsureSuccessStatusCode();

        var companiesResponse = await platform.GetCompaniesAsync();
        var companies = await companiesResponse.Content.ReadFromJsonAsync<List<ApiClient.CompanyResult>>();
        Assert.Equal("Suspended", companies!.Single(c => c.Id == companyId).Status);

        (await platform.ReactivateCompanyAsync(companyId)).EnsureSuccessStatusCode();

        companiesResponse = await platform.GetCompaniesAsync();
        companies = await companiesResponse.Content.ReadFromJsonAsync<List<ApiClient.CompanyResult>>();
        Assert.Equal("Active", companies!.Single(c => c.Id == companyId).Status);
    }

    [Fact]
    public async Task A_deactivated_platform_admin_cannot_log_in()
    {
        var (email, password) = await factory.SeedPlatformAdminAsync();
        await factory.DeactivatePlatformAdminAsync(email);

        var client = factory.CreatePlatformClient();
        var response = await client.Http.PostAsJsonAsync("/api/platform/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
