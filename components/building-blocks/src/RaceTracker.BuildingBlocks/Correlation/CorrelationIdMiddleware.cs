using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace RaceTracker.BuildingBlocks.Correlation;

/// <summary>
/// Reads (or generates) a correlation id per request, echoes it on the response and
/// pushes it into the Serilog <see cref="LogContext"/> so every log line of the flow
/// is traceable (<c>/A80/</c>). The device <c>guid</c> stays the service-spanning
/// correlation key end to end; this handles the HTTP edge.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogContextPropertyName = "CorrelationId";
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context.Request);
        context.Items[ItemsKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty(LogContextPropertyName, correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpRequest request)
    {
        if (request.Headers.TryGetValue(HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
