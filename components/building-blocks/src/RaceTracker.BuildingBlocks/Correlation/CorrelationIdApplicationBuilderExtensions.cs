using Microsoft.AspNetCore.Builder;

namespace RaceTracker.BuildingBlocks.Correlation;

public static class CorrelationIdApplicationBuilderExtensions
{
    /// <summary>Adds the <see cref="CorrelationIdMiddleware"/> to the pipeline.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
