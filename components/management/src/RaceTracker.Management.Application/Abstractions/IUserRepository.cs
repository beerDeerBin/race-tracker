using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Application.Abstractions;

/// <summary>
/// Port for the user store (<c>/D10/</c>). The real adapter is backed by MongoDB; unit tests mock
/// it. Lookups are by the (unique) login name; seeding is idempotent so a restart never duplicates
/// or overwrites the existing account.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns the user with the given login name, or <c>null</c> if none exists.</summary>
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts <paramref name="user"/> only if no user with its <see cref="User.Username"/> exists
    /// yet (single-user seeding, <c>/O20/</c>). Idempotent: a no-op when the account is present.
    /// </summary>
    Task EnsureSeededAsync(User user, CancellationToken cancellationToken);
}
