using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RaceTracker.Gateway.UnitTests.Support;

/// <summary>
/// Test helper that listens to a named <see cref="Meter"/> via a real
/// <see cref="MeterListener"/> and sums <see cref="long"/> counter measurements by instrument
/// name — so tests assert against the actual metrics pipeline, not a mock.
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, long> _totals = new(StringComparer.Ordinal);

    public MetricsCollector(string meterName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            _totals.AddOrUpdate(instrument.Name, measurement, (_, current) => current + measurement));

        _listener.Start();
    }

    /// <summary>The summed count for an instrument, or 0 if it never fired.</summary>
    public long Total(string instrumentName) =>
        _totals.TryGetValue(instrumentName, out long value) ? value : 0;

    public void Dispose() => _listener.Dispose();
}
