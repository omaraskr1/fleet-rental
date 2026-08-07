using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;

namespace FleetRental.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void RegisterClient_creates_a_client_not_an_admin()
    {
        var user = User.RegisterClient("a@b.com", "hash", "Amira Hassan");

        Assert.Equal(UserRole.Client, user.Role);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void RegisterAdmin_creates_an_admin()
    {
        Assert.Equal(UserRole.Admin, User.RegisterAdmin("a@b.com", "hash", "Owner").Role);
    }

    [Theory]
    [InlineData("Amira@Example.COM", "amira@example.com")]
    [InlineData("  spaced@x.com  ", "spaced@x.com")]
    [InlineData("ALLCAPS@X.COM", "allcaps@x.com")]
    public void Email_is_normalised_on_registration(string input, string expected)
    {
        // Login looks the address up in normalised form. If registration stored it
        // any other way, the account would exist but never be findable.
        Assert.Equal(expected, User.RegisterClient(input, "hash", "N").Email);
    }

    [Fact]
    public void NormalizeEmail_matches_what_registration_stores()
    {
        const string raw = "  Mixed.Case@Example.COM ";
        Assert.Equal(User.RegisterClient(raw, "h", "N").Email, User.NormalizeEmail(raw));
    }

    [Theory]
    [InlineData("", "hash", "Name")]
    [InlineData("   ", "hash", "Name")]
    [InlineData("a@b.com", "", "Name")]
    [InlineData("a@b.com", "hash", "")]
    [InlineData("a@b.com", "hash", "   ")]
    public void Registration_requires_email_hash_and_name(string email, string hash, string name)
    {
        Assert.Throws<DomainException>(() => User.RegisterClient(email, hash, name));
    }

    [Fact]
    public void Deactivate_and_reactivate_toggle_access()
    {
        var user = User.RegisterClient("a@b.com", "hash", "N");

        user.Deactivate();
        Assert.False(user.IsActive);

        user.Reactivate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void UpdateProfile_trims_and_requires_a_name()
    {
        var user = User.RegisterClient("a@b.com", "hash", "Old");

        user.UpdateProfile("  New Name  ", "  +971500000000  ");

        Assert.Equal("New Name", user.FullName);
        Assert.Equal("+971500000000", user.PhoneNumber);
        Assert.Throws<DomainException>(() => user.UpdateProfile("  ", null));
    }

    // ---------- Device tokens ----------

    [Fact]
    public void Registering_the_same_device_twice_updates_rather_than_duplicates()
    {
        var user = User.RegisterClient("a@b.com", "hash", "N");

        user.RegisterDeviceToken("token-1", DevicePlatform.Ios, "device-A");
        user.RegisterDeviceToken("token-2", DevicePlatform.Ios, "device-A");

        // Duplicates would mean the user receives every push twice.
        Assert.Single(user.DeviceTokens);
        Assert.Equal("token-2", user.DeviceTokens.Single().Token);
    }

    [Fact]
    public void Different_devices_are_tracked_separately()
    {
        var user = User.RegisterClient("a@b.com", "hash", "N");

        user.RegisterDeviceToken("t1", DevicePlatform.Ios, "phone");
        user.RegisterDeviceToken("t2", DevicePlatform.Android, "tablet");

        Assert.Equal(2, user.DeviceTokens.Count);
    }

    [Fact]
    public void RemoveDeviceToken_removes_only_the_named_device()
    {
        var user = User.RegisterClient("a@b.com", "hash", "N");
        user.RegisterDeviceToken("t1", DevicePlatform.Ios, "phone");
        user.RegisterDeviceToken("t2", DevicePlatform.Android, "tablet");

        user.RemoveDeviceToken("phone");

        Assert.Single(user.DeviceTokens);
        Assert.Equal("tablet", user.DeviceTokens.Single().DeviceId);
    }

    [Fact]
    public void Removing_an_unknown_device_is_a_no_op()
    {
        var user = User.RegisterClient("a@b.com", "hash", "N");
        user.RemoveDeviceToken("never-registered");
        Assert.Empty(user.DeviceTokens);
    }
}

public class EventTests
{
    private static readonly Guid Organizer = Guid.CreateVersion7();

    [Fact]
    public void Create_populates_and_trims_details()
    {
        var ev = Event.Create(Organizer, "  Expo  ", EventType.TradeShow, "  Hall 1  ", 500, "  notes  ");

        Assert.Equal("Expo", ev.Name);
        Assert.Equal("Hall 1", ev.Location);
        Assert.Equal("notes", ev.Notes);
        Assert.Equal(500, ev.ExpectedAttendance);
        Assert.Equal(Organizer, ev.OrganizerId);
    }

    [Theory]
    [InlineData("", "Location")]
    [InlineData("   ", "Location")]
    [InlineData("Name", "")]
    [InlineData("Name", "   ")]
    public void Create_requires_name_and_location(string name, string location)
    {
        Assert.Throws<DomainException>(
            () => Event.Create(Organizer, name, EventType.Other, location));
    }

    [Fact]
    public void Create_rejects_negative_attendance()
    {
        Assert.Throws<DomainException>(
            () => Event.Create(Organizer, "N", EventType.Other, "L", -1));
    }

    [Fact]
    public void Attendance_is_optional()
    {
        Assert.Null(Event.Create(Organizer, "N", EventType.Other, "L").ExpectedAttendance);
    }

    [Fact]
    public void UpdateDetails_applies_the_same_rules_as_Create()
    {
        var ev = Event.Create(Organizer, "N", EventType.Other, "L");

        ev.UpdateDetails("New", EventType.Wedding, "Beach", 80, "note");

        Assert.Equal("New", ev.Name);
        Assert.Equal(EventType.Wedding, ev.Type);
        Assert.Throws<DomainException>(() => ev.UpdateDetails("", EventType.Other, "L", null, null));
    }
}
