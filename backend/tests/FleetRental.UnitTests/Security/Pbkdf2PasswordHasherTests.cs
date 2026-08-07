using FleetRental.Infrastructure.Security;

namespace FleetRental.UnitTests.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hash = _hasher.Hash("CorrectHorse123");
        Assert.True(_hasher.Verify("CorrectHorse123", hash));
    }

    [Fact]
    public void Verify_fails_for_the_wrong_password()
    {
        var hash = _hasher.Hash("CorrectHorse123");
        Assert.False(_hasher.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Verify_is_case_sensitive()
    {
        var hash = _hasher.Hash("CorrectHorse123");
        Assert.False(_hasher.Verify("correcthorse123", hash));
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        // Per-password random salt. Identical hashes would reveal that two users
        // share a password, and make the whole table rainbow-table-able at once.
        var a = _hasher.Hash("SamePassword1");
        var b = _hasher.Hash("SamePassword1");

        Assert.NotEqual(a, b);
        Assert.True(_hasher.Verify("SamePassword1", a));
        Assert.True(_hasher.Verify("SamePassword1", b));
    }

    [Fact]
    public void Hash_embeds_the_iteration_count_so_it_can_be_raised_later()
    {
        // Format is {iterations}.{salt}.{hash}. Without the count baked in, raising
        // the work factor would invalidate every existing password.
        var parts = _hasher.Hash("Password123").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.True(iterations >= 100_000, $"work factor too low: {iterations}");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-hash")]
    [InlineData("abc.def")]
    [InlineData("notanumber.c2FsdA==.aGFzaA==")]
    [InlineData("1000.!!!not-base64!!!.aGFzaA==")]
    public void Verify_returns_false_for_a_malformed_hash_rather_than_throwing(string hash)
    {
        // A corrupted row must fail the login, not take down the endpoint with a 500.
        Assert.False(_hasher.Verify("anything", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_returns_false_for_an_empty_password(string password)
    {
        var hash = _hasher.Hash("RealPassword1");
        Assert.False(_hasher.Verify(password, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Hash_rejects_an_empty_password(string password)
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(password));
    }

    [Fact]
    public void Handles_unicode_and_long_passwords()
    {
        const string password = "مرحبا-كلمة-السر-🚗-very-long-passphrase-with-mixed-content-123";
        Assert.True(_hasher.Verify(password, _hasher.Hash(password)));
    }
}
