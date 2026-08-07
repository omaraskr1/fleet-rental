using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;
using FleetRental.Domain.Enums;
using FleetRental.Domain.ValueObjects;

namespace FleetRental.UnitTests.Domain;

public class CarTests
{
    private static Car NewCar() => Car.Create("V-Class", "Roomy van", CarCategory.Van, 8, 320m);

    // ---------- Validation ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_requires_a_name(string? name)
    {
        Assert.Throws<DomainException>(
            () => Car.Create(name!, "d", CarCategory.Sedan, 4, 100m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_requires_positive_seats(int seats)
    {
        Assert.Throws<DomainException>(
            () => Car.Create("X", "d", CarCategory.Sedan, seats, 100m));
    }

    [Fact]
    public void Create_rejects_a_negative_rate()
    {
        Assert.Throws<DomainException>(
            () => Car.Create("X", "d", CarCategory.Sedan, 4, -1m));
    }

    [Fact]
    public void Create_allows_a_zero_rate_for_sponsored_vehicles()
    {
        var car = Car.Create("Sponsored", "d", CarCategory.Sedan, 4, 0m);
        Assert.Equal(0m, car.DailyRate);
    }

    [Fact]
    public void Create_trims_whitespace()
    {
        var car = Car.Create("  Range Rover  ", "  Big  ", CarCategory.Suv, 5, 380m, "  D-12345  ");

        Assert.Equal("Range Rover", car.Name);
        Assert.Equal("Big", car.Description);
        Assert.Equal("D-12345", car.LicensePlate);
    }

    // ---------- Status ----------

    [Theory]
    [InlineData(CarStatus.Active, true)]
    [InlineData(CarStatus.Maintenance, false)]
    [InlineData(CarStatus.Retired, false)]
    public void IsBookable_only_when_active(CarStatus status, bool expected)
    {
        var car = NewCar();
        car.ChangeStatus(status);
        Assert.Equal(expected, car.IsBookable);
    }

    // ---------- Photos ----------

    [Fact]
    public void First_photo_becomes_primary_automatically()
    {
        var car = NewCar();

        var photo = car.AddPhoto("https://example.com/1.jpg");

        // Without this the listing screen would render a card with no image.
        Assert.True(photo.IsPrimary);
        Assert.Equal(photo, car.PrimaryPhoto);
    }

    [Fact]
    public void Only_one_photo_is_primary_at_a_time()
    {
        var car = NewCar();
        car.AddPhoto("https://example.com/1.jpg");
        car.AddPhoto("https://example.com/2.jpg");

        car.AddPhoto("https://example.com/3.jpg", isPrimary: true);

        Assert.Single(car.Photos, p => p.IsPrimary);
        Assert.Equal("https://example.com/3.jpg", car.PrimaryPhoto!.Url);
    }

    [Fact]
    public void Removing_the_primary_promotes_the_next_photo()
    {
        var car = NewCar();
        var first = car.AddPhoto("https://example.com/1.jpg");
        car.AddPhoto("https://example.com/2.jpg");

        car.RemovePhoto(first.Id);

        // A car with photos but no primary would render blank on the listing.
        Assert.NotNull(car.PrimaryPhoto);
        Assert.Equal("https://example.com/2.jpg", car.PrimaryPhoto!.Url);
    }

    [Fact]
    public void Removing_the_only_photo_leaves_none()
    {
        var car = NewCar();
        var only = car.AddPhoto("https://example.com/1.jpg");

        car.RemovePhoto(only.Id);

        Assert.Empty(car.Photos);
        Assert.Null(car.PrimaryPhoto);
    }

    [Fact]
    public void Removing_an_unknown_photo_is_a_no_op()
    {
        var car = NewCar();
        car.AddPhoto("https://example.com/1.jpg");

        car.RemovePhoto(Guid.CreateVersion7());

        Assert.Single(car.Photos);
    }

    [Fact]
    public void AddPhoto_requires_a_url()
    {
        Assert.Throws<DomainException>(() => NewCar().AddPhoto("  "));
    }

    // ---------- Availability ----------

    [Fact]
    public void A_car_with_no_bookings_is_available()
    {
        var range = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5));
        Assert.True(NewCar().IsAvailableFor(range));
    }

    // ---------- Maintenance ----------

    [Fact]
    public void A_new_car_tracks_no_odometer_or_interval_by_default()
    {
        var car = NewCar();

        // Null, not zero — a car nobody has ever entered a reading for must not
        // be indistinguishable from one that is at 0 km.
        Assert.Null(car.CurrentOdometerKm);
        Assert.Null(car.ServiceIntervalKm);
    }

    [Fact]
    public void UpdateOdometer_records_the_reading()
    {
        var car = NewCar();
        car.UpdateOdometer(45_000);
        Assert.Equal(45_000, car.CurrentOdometerKm);
    }

    [Fact]
    public void UpdateOdometer_rejects_a_negative_reading()
    {
        Assert.Throws<DomainException>(() => NewCar().UpdateOdometer(-1));
    }

    [Fact]
    public void UpdateOdometer_rejects_a_reading_lower_than_the_one_on_file()
    {
        var car = NewCar();
        car.UpdateOdometer(45_000);

        // Almost always a typo, and silently accepting it would corrupt the
        // "km since last service" calculation with a value that moves backwards.
        var ex = Assert.Throws<DomainException>(() => car.UpdateOdometer(44_000));
        Assert.Contains("lower than the current one on file", ex.Message);
    }

    [Fact]
    public void UpdateOdometer_allows_the_same_reading_twice()
    {
        var car = NewCar();
        car.UpdateOdometer(45_000);
        car.UpdateOdometer(45_000);
        Assert.Equal(45_000, car.CurrentOdometerKm);
    }

    [Fact]
    public void SetServiceInterval_records_the_distance()
    {
        var car = NewCar();
        car.SetServiceInterval(10_000);
        Assert.Equal(10_000, car.ServiceIntervalKm);
    }

    [Fact]
    public void SetServiceInterval_null_turns_tracking_off()
    {
        var car = NewCar();
        car.SetServiceInterval(10_000);

        car.SetServiceInterval(null);

        Assert.Null(car.ServiceIntervalKm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetServiceInterval_rejects_a_non_positive_value(int km)
    {
        Assert.Throws<DomainException>(() => NewCar().SetServiceInterval(km));
    }
}
