using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;

namespace FleetRental.UnitTests.Domain;

public class ServiceTypeTests
{
    private static ServiceType OilChange() => ServiceType.Create("Oil change", 10_000);

    [Fact]
    public void Create_starts_active()
    {
        var type = OilChange();

        Assert.Equal("Oil change", type.Name);
        Assert.Equal(10_000, type.IntervalKm);
        Assert.True(type.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_requires_a_name(string? name)
    {
        Assert.Throws<DomainException>(() => ServiceType.Create(name!, 10_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_requires_a_positive_interval(int intervalKm)
    {
        Assert.Throws<DomainException>(() => ServiceType.Create("Oil change", intervalKm));
    }

    [Fact]
    public void Create_trims_the_name()
    {
        var type = ServiceType.Create("  Tire rotation  ", 8_000);
        Assert.Equal("Tire rotation", type.Name);
    }

    [Fact]
    public void UpdateDetails_changes_name_and_interval()
    {
        var type = OilChange();

        type.UpdateDetails("Synthetic oil change", 12_000);

        Assert.Equal("Synthetic oil change", type.Name);
        Assert.Equal(12_000, type.IntervalKm);
    }

    [Fact]
    public void UpdateDetails_rejects_a_non_positive_interval()
    {
        var type = OilChange();
        Assert.Throws<DomainException>(() => type.UpdateDetails("Oil change", 0));
    }

    [Fact]
    public void Deactivate_then_Reactivate_round_trips()
    {
        var type = OilChange();

        type.Deactivate();
        Assert.False(type.IsActive);

        type.Reactivate();
        Assert.True(type.IsActive);
    }
}
