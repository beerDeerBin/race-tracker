using System.Security.Claims;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Auth;

namespace RaceTracker.Management.Api.Auth;

/// <summary>
/// Auth HTTP surface (minimal API, consistent with the other services; the MVC generic
/// <c>Controller&lt;T&gt;</c> arrives with CRUD in 5.3). <c>POST /login</c> is the only opened
/// endpoint (<c>AllowAnonymous</c>); <c>GET /me</c> carries no authorization metadata, so the
/// fallback policy protects it — the worked example of secure-by-default (<c>/F11/</c>, <c>/F12/</c>).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/login", LoginAsync).AllowAnonymous();
        endpoints.MapGet("/me", Me);
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AuthenticationService authentication,
        CancellationToken cancellationToken)
    {
        AccessToken? token = await authentication.AuthenticateAsync(
            request.Username, request.Password, cancellationToken);

        return token is null
            ? Results.Unauthorized()
            : Results.Ok(new LoginResponse(token.Value, "Bearer", token.ExpiresAt));
    }

    // Only reached with a valid token (fallback authorization policy); echoes the principal.
    private static IResult Me(ClaimsPrincipal user) =>
        Results.Ok(new MeResponse(user.Identity?.Name ?? string.Empty, user.FindFirstValue(JwtClaimNames.Role)));
}

/// <summary>Login credentials (API boundary DTO, kept separate from the <c>User</c> entity).</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>The minted bearer token returned on a successful login.</summary>
public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

/// <summary>The authenticated principal, returned by the protected sample endpoint.</summary>
public sealed record MeResponse(string Username, string? Role);
