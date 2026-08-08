using FleetRental.Domain.Common;
using FleetRental.Domain.Entities;

namespace FleetRental.UnitTests.Domain;

public class ServiceRecordTests
{
    private static readonly Guid CarId = Guid.CreateVersion7();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    [Fact]
    public void Log_records_the_given_details()
    {
        var record = ServiceRecord.Log(CarId, Today, "Oil and filter change", 45_000, 250m, "Al Futtaim Service");

        Assert.Equal(CarId, record.CarId);
        Assert.Equal(Today, record.PerformedAt);
        Assert.Equal("Oil and filter change", record.Description);
        Assert.Equal(45_000, record.OdometerKm);
        Assert.Equal(250m, record.Cost);
        Assert.Equal("Al Futtaim Service", record.PerformedBy);
    }

    [Fact]
    public void Odometer_and_performedBy_are_optional()
    {
        var record = ServiceRecord.Log(CarId, Today, "Windscreen chip repair", null, 80m);

        Assert.Null(record.OdometerKm);
        Assert.Null(record.PerformedBy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Description_is_required(string description)
    {
        Assert.Throws<DomainException>(() => ServiceRecord.Log(CarId, Today, description, null, 100m));
    }

    [Fact]
    public void Negative_odometer_is_rejected()
    {
        Assert.Throws<DomainException>(() => ServiceRecord.Log(CarId, Today, "Service", -1, 100m));
    }

    [Fact]
    public void Negative_cost_is_rejected()
    {
        Assert.Throws<DomainException>(() => ServiceRecord.Log(CarId, Today, "Service", 1000, -1m));
    }

    [Fact]
    public void Zero_cost_is_allowed_for_warranty_work()
    {
        var record = ServiceRecord.Log(CarId, Today, "Warranty recall", 1000, 0m);
        Assert.Equal(0m, record.Cost);
    }

    [Fact]
    public void A_future_service_date_is_rejected()
    {
        // Silently accepting this would corrupt the next-service-due calculation,
        // which trusts the most recent PerformedAt to mean "already happened."
        var tomorrow = Today.AddDays(1);

        var ex = Assert.Throws<DomainException>(() => ServiceRecord.Log(CarId, tomorrow, "Service", 1000, 100m));
        Assert.Contains("cannot be in the future", ex.Message);
    }

    [Fact]
    public void Todays_date_is_allowed()
    {
        var record = ServiceRecord.Log(CarId, Today, "Same-day service", 1000, 100m);
        Assert.Equal(Today, record.PerformedAt);
    }
}
