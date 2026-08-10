using System.Net;
using System.Net.Http.Json;
using FleetRental.Domain.Enums;

namespace FleetRental.IntegrationTests;

/// <summary>
/// Feature 6 — notification on approve/reject. Nothing previously exercised
/// <c>BookingNotificationService</c> end to end: these tests confirm the
/// pipeline actually marks a decision notified, that a registered push device
/// is walked without blowing up the approval request, and that disabling the
/// per-company Push toggle degrades to email-only rather than blocking the
/// decision (or the notification) entirely.
/// </summary>
[Collection(nameof(ApiCollection))]
public class NotificationTests(FleetRentalApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync() => await factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Approving_a_booking_marks_it_notified()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync(tenantId);

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("notify-approve@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(
            carId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));
        (await admin.ApproveAsync(bookingId)).EnsureSuccessStatusCode();

        Assert.True(await factory.IsBookingNotifiedAsync(bookingId));
    }

    [Fact]
    public async Task Rejecting_a_booking_marks_it_notified()
    {
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync(tenantId);

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("notify-reject@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(
            carId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));
        (await admin.RejectAsync(bookingId, "No availability")).EnsureSuccessStatusCode();

        Assert.True(await factory.IsBookingNotifiedAsync(bookingId));
    }

    [Fact]
    public async Task A_client_with_a_registered_push_device_still_gets_approved_and_notified()
    {
        // Exercises PushNotificationSender's real device-lookup path (not just
        // the "no devices, skip" branch every other test hits implicitly).
        var tenantId = await factory.SeedTenantAsync();
        var carId = await factory.SeedCarAsync(tenantId);
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync(tenantId);

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("notify-push@test.com");
        var registerResponse = await client.Http.PostAsJsonAsync("/api/auth/devices", new
        {
            token = "device-token-abc",
            platform = "Android",
            deviceId = "device-1",
        });
        registerResponse.EnsureSuccessStatusCode();

        var bookingId = await client.CreateBookingAndGetIdAsync(
            carId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        var approveResponse = await admin.ApproveAsync(bookingId);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.True(await factory.IsBookingNotifiedAsync(bookingId));
    }

    [Fact]
    public async Task Disabling_push_notifications_still_lets_approval_notify_by_email()
    {
        // Only the Push channel is gated by the toggle (see
        // BookingNotificationService.NotifyDecisionAsync) — email is never
        // gated, so a company with Push off must still see the booking marked
        // notified once the email channel delivers.
        var tenantId = await factory.SeedTenantAsync();
        await factory.SetFeatureAsync(tenantId, FeatureKey.PushNotifications, isEnabled: false);
        var carId = await factory.SeedCarAsync(tenantId);
        var (adminEmail, adminPassword) = await factory.SeedAdminAsync(tenantId);

        var client = factory.CreateTenantClient();
        await client.SignUpAndAuthenticateAsync("notify-push-off@test.com");
        var bookingId = await client.CreateBookingAndGetIdAsync(
            carId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));

        var admin = factory.CreateTenantClient();
        admin.Authenticate(await admin.LoginAsync(adminEmail, adminPassword, asAdmin: true));

        var approveResponse = await admin.ApproveAsync(bookingId);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        Assert.True(await factory.IsBookingNotifiedAsync(bookingId));
    }
}
