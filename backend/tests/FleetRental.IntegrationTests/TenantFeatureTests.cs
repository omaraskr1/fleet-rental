using System.Net;
using System.Net.Http.Json;
using FleetRental.Domain.Enums;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Per-company feature toggles: a platform admin can turn a feature off for one
/// company, the gate enforces it server-side (not just hides a nav link), and the
/// toggle never bleeds across tenants.
/// </summary>
[Collection(nameof(ApiCollection))]
public class TenantFeatureTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync() => await factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_feature_with_no_override_is_enabled_by_default()
    {
        var tenantId = await factory.SeedTenantAsync();
        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var response = await admin.Http.GetAsync("/api/analytics/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Disabling_analytics_returns_forbidden_from_every_analytics_endpoint()
    {
        var tenantId = await factory.SeedTenantAsync();
        await factory.SetFeatureAsync(tenantId, FeatureKey.Analytics, isEnabled: false);
        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var overview = await admin.Http.GetAsync("/api/analytics/overview");
        var revenue = await admin.Http.GetAsync("/api/analytics/revenue");

        Assert.Equal(HttpStatusCode.Forbidden, overview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revenue.StatusCode);
    }

    [Fact]
    public async Task Disabling_maintenance_returns_forbidden_from_service_and_issue_endpoints()
    {
        var tenantId = await factory.SeedTenantAsync();
        await factory.SetFeatureAsync(tenantId, FeatureKey.Maintenance, isEnabled: false);
        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var serviceTypes = await admin.GetServiceTypesAsync();
        var issues = await admin.Http.GetAsync("/api/issues");

        Assert.Equal(HttpStatusCode.Forbidden, serviceTypes.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, issues.StatusCode);
    }

    [Fact]
    public async Task Disabling_a_feature_for_one_company_does_not_affect_another()
    {
        var alpha = await factory.SeedTenantAsync("alpha-features");
        var beta = await factory.SeedTenantAsync("beta-features");
        await factory.SetFeatureAsync(alpha, FeatureKey.Analytics, isEnabled: false);

        var (alphaEmail, alphaPassword) = await factory.SeedAdminAsync(alpha, "alpha-admin@test.com");
        var (betaEmail, betaPassword) = await factory.SeedAdminAsync(beta, "beta-admin@test.com");

        var alphaClient = factory.CreateTenantClient("alpha-features");
        alphaClient.Authenticate(await alphaClient.LoginAsync(alphaEmail, alphaPassword, asAdmin: true));

        var betaClient = factory.CreateTenantClient("beta-features");
        betaClient.Authenticate(await betaClient.LoginAsync(betaEmail, betaPassword, asAdmin: true));

        var alphaResponse = await alphaClient.Http.GetAsync("/api/analytics/overview");
        var betaResponse = await betaClient.Http.GetAsync("/api/analytics/overview");

        Assert.Equal(HttpStatusCode.Forbidden, alphaResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, betaResponse.StatusCode);
    }

    [Fact]
    public async Task GET_features_reflects_the_current_tenants_overrides_to_a_signed_in_user()
    {
        var tenantId = await factory.SeedTenantAsync();
        await factory.SetFeatureAsync(tenantId, FeatureKey.Maintenance, isEnabled: false);

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("browsing@test.com");

        var response = await client.Http.GetAsync("/api/features");
        response.EnsureSuccessStatusCode();
        var toggles = await response.Content.ReadFromJsonAsync<List<FeatureToggleResult>>();
        Assert.NotNull(toggles);

        Assert.True(toggles.Single(t => t.Key == "Analytics").IsEnabled);
        Assert.False(toggles.Single(t => t.Key == "Maintenance").IsEnabled);
    }

    [Fact]
    public async Task A_platform_admin_can_list_and_set_a_companys_feature_toggles()
    {
        var (platformEmail, platformPassword) = await factory.SeedPlatformAdminAsync();
        var platform = factory.CreatePlatformClient();
        platform.Authenticate(await platform.PlatformLoginAsync(platformEmail, platformPassword));

        var tenantId = await platform.CreateCompanyAndGetIdAsync("Feature Toggle Co", "feature-toggle-co");

        var listResponse = await platform.Http.GetAsync($"/api/platform/companies/{tenantId}/features");
        listResponse.EnsureSuccessStatusCode();
        var toggles = await listResponse.Content.ReadFromJsonAsync<List<FeatureToggleResult>>();
        Assert.NotNull(toggles);
        Assert.All(toggles, t => Assert.True(t.IsEnabled)); // every key defaults on

        var setResponse = await platform.Http.PutAsJsonAsync(
            $"/api/platform/companies/{tenantId}/features/Gps", new { isEnabled = false });
        setResponse.EnsureSuccessStatusCode();

        listResponse = await platform.Http.GetAsync($"/api/platform/companies/{tenantId}/features");
        toggles = await listResponse.Content.ReadFromJsonAsync<List<FeatureToggleResult>>();
        Assert.NotNull(toggles);
        Assert.False(toggles.Single(t => t.Key == "Gps").IsEnabled);
        Assert.True(toggles.Single(t => t.Key == "Analytics").IsEnabled);
    }

    [Fact]
    public async Task A_tenant_admin_cannot_set_their_own_companys_feature_toggles()
    {
        // Only a platform admin may do this — a tenant admin re-enabling their own
        // disabled feature would defeat the whole point of the toggle.
        var tenantId = await factory.SeedTenantAsync();
        var (email, password) = await factory.SeedAdminAsync(tenantId);
        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(email, password, asAdmin: true));

        var response = await admin.Http.PutAsJsonAsync(
            $"/api/platform/companies/{tenantId}/features/Analytics", new { isEnabled = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public record FeatureToggleResult(string Key, bool IsEnabled);
}
