namespace FleetRental.IntegrationTests;

/// <summary>
/// Shares one API host and one test database across all integration tests.
/// </summary>
/// <remarks>
/// xUnit runs collections in parallel by default. These tests all reset and
/// re-seed the same database, so they must run in sequence — a shared collection
/// is what serialises them. Without it, one test's ResetAsync would delete another
/// test's fixtures mid-run and the failures would look random.
/// </remarks>
[CollectionDefinition(nameof(ApiCollection), DisableParallelization = true)]
public class ApiCollection : ICollectionFixture<FleetRentalApiFactory>;
