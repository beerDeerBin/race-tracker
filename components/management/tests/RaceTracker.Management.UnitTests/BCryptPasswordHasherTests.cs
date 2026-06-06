using RaceTracker.Management.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Exercises the real BCrypt hasher directly (no I/O, so it fits the unit tier): a password
/// round-trips, a wrong password is rejected, and the per-password salt makes two hashes of the
/// same input differ (§8 Sicherheit).
/// </summary>
public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_verify_accepts_the_correct_password()
    {
        string hash = _hasher.Hash("s3cret-password");

        _hasher.Verify("s3cret-password", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_rejects_a_wrong_password()
    {
        string hash = _hasher.Hash("s3cret-password");

        _hasher.Verify("not-the-password", hash).ShouldBeFalse();
    }

    [Fact]
    public void Hashing_the_same_password_twice_yields_different_salted_hashes()
    {
        string first = _hasher.Hash("same-password");
        string second = _hasher.Hash("same-password");

        first.ShouldNotBe(second);
        first.ShouldNotBe("same-password"); // never stores the plaintext
    }
}
