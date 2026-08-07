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

        Assert.Single(car.Photos.Where(p => p.IsPrimary));
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
}
