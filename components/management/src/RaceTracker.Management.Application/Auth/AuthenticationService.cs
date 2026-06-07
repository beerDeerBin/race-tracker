using Microsoft.Extensions.Logging;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Application.Auth;

/// <summary>
/// Login use case (<c>/F11/</c>): looks the user up by login name, verifies the supplied password
/// against the stored hash, and on success mints a signed bearer token. A missing user and a wrong
/// password yield the <b>same</b> negative result (<c>null</c>) so the outcome does not reveal
/// whether the account exists (no user enumeration).
/// </summary>
public sealed partial class AuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenIssuer tokenIssuer,
        ILogger<AuthenticationService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates the credentials and returns a signed token, or <c>null</c> when the user is
    /// unknown or the password does not match.
    /// </summary>
    public async Task<AccessToken?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        User? user = await _users.FindByUsernameAsync(username, cancellationToken);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            LogLoginFailed(username);
            return null;
        }

        AccessToken token = _tokenIssuer.Issue(user);
        LogLoginSucceeded(username);
        return token;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Login failed for user {Username}.")]
    private partial void LogLoginFailed(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Username} authenticated.")]
    private partial void LogLoginSucceeded(string username);
}
