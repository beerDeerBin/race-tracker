using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Domain.Auth;

namespace RaceTracker.Management.Application.Auth;

/// <summary>
/// Seeds the single user (<c>/O20/</c>) at startup so a fresh deployment can immediately log in.
/// The configured plaintext password is hashed once here (§8 Sicherheit) and the account is
/// inserted only if absent (idempotent). Runs with bounded retry/backoff because MongoDB may not
/// be reachable the instant the service starts (resilience baseline), mirroring the persistence
/// migration startup.
/// </summary>
public sealed partial class UserSeeder : IHostedService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(3);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly SeedUserOptions _seedUser;
    private readonly ILogger<UserSeeder> _logger;

    public UserSeeder(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider,
        IOptions<AuthOptions> options,
        ILogger<UserSeeder> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _seedUser = options.Value.SeedUser;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        User user = new()
        {
            Id = Guid.NewGuid().ToString(),
            Username = _seedUser.Username,
            PasswordHash = _passwordHasher.Hash(_seedUser.Password),
            Role = _seedUser.Role,
            CreatedAt = _timeProvider.GetUtcNow(),
        };

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await _users.EnsureSeededAsync(user, cancellationToken);
                LogSeeded(_seedUser.Username);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxAttempts)
            {
                LogSeedAttemptFailed(ex, attempt, MaxAttempts, _retryDelay.TotalSeconds);
                await Task.Delay(_retryDelay, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed user {Username} ensured.")]
    private partial void LogSeeded(string username);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Seeding the user failed (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}s.")]
    private partial void LogSeedAttemptFailed(Exception exception, int attempt, int maxAttempts, double delay);
}
