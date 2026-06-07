using Microsoft.AspNetCore.Http;
using RaceTracker.BuildingBlocks.Correlation;
using Shouldly;
using Xunit;

namespace RaceTracker.Gateway.UnitTests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Generates_a_correlation_id_when_the_header_is_absent()
    {
        var context = new DefaultHttpContext();
        string? captured = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            captured = ctx.Items[CorrelationIdMiddleware.ItemsKey] as string;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        captured.ShouldNotBeNullOrWhiteSpace();
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(captured);
    }

    [Fact]
    public async Task Propagates_an_incoming_correlation_id()
    {
        const string incoming = "abc-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Items[CorrelationIdMiddleware.ItemsKey].ShouldBe(incoming);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().ShouldBe(incoming);
    }
}
