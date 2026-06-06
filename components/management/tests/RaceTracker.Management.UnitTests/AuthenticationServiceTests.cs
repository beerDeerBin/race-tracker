using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Auth;
using RaceTracker.Management.Domain.Auth;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the login use case (<c>/F11/</c>) with all ports mocked. Proves a token is minted
/// only for valid credentials and that an unknown user and a wrong password fail identically
/// (no token, no user enumeration).
/// </summary>
public sealed class AuthenticationServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenIssuer _tokenIssuer = Substitute.For<ITokenIssuer>();

    private AuthenticationService CreateSut() =>
        new(_users, _passwordHasher, _tokenIssuer, NullLogger<AuthenticationService>.Instance);

    private static User SeededUser() => new()
    {
        Id = "user-1",
        Username = "admin",
        PasswordHash = "stored-hash",
        Role = "admin",
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Valid_credentials_return_the_issued_token()
    {
        User user = SeededUser();
        var expected = new AccessToken("signed.jwt.value", DateTimeOffset.UnixEpoch.AddHours(1));
        _users.FindByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct", "stored-hash").Returns(true);
        _tokenIssuer.Issue(user).Returns(expected);

        AccessToken? result = await CreateSut().AuthenticateAsync("admin", "correct", CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Wrong_password_fails_without_issuing_a_token()
    {
        _users.FindByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(SeededUser());
        _passwordHasher.Verify("wrong", "stored-hash").Returns(false);

        AccessToken? result = await CreateSut().AuthenticateAsync("admin", "wrong", CancellationToken.None);

        result.ShouldBeNull();
        _tokenIssuer.DidNotReceive().Issue(Arg.Any<User>());
    }

    [Fact]
    public async Task Unknown_user_fails_without_checking_the_password_or_issuing_a_token()
    {
        _users.FindByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        AccessToken? result = await CreateSut().AuthenticateAsync("ghost", "whatever", CancellationToken.None);

        result.ShouldBeNull();
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
        _tokenIssuer.DidNotReceive().Issue(Arg.Any<User>());
    }
}
