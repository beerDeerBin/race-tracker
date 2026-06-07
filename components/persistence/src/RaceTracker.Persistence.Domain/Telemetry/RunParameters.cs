namespace RaceTracker.Persistence.Domain.Telemetry;

/// <summary>
/// Validated run parameters announced by management at <c>START_RUN</c> (/F54/), ready to upsert into
/// the run-metadata columns the telemetry path cannot supply — above all <see cref="OdrHz"/>, the
/// time base for the run's samples (<c>t = index / odr_hz</c>). Built through <see cref="Create"/>,
/// which enforces the domain invariants and throws <see cref="TelemetryValidationException"/> on any
/// violation, so a malformed announcement is rejected at the boundary and dead-lettered rather than
/// written. Keyed (like the samples) by <see cref="DeviceGuid"/> + <see cref="RunId"/>.
/// </summary>
public sealed class RunParameters
{
    private RunParameters(
        Guid deviceGuid, Guid runId, int numSamples, int odrHz,
        short accelRange, short gyroRange, DateTimeOffset startedAtUtc)
    {
        DeviceGuid = deviceGuid;
        RunId = runId;
        NumSamples = numSamples;
        OdrHz = odrHz;
        AccelRange = accelRange;
        GyroRange = gyroRange;
        StartedAtUtc = startedAtUtc;
    }

    /// <summary>Device GUID (cross-service correlation key) — the run's <c>device_guid</c>.</summary>
    public Guid DeviceGuid { get; }

    /// <summary>Run UUID — the run's <c>run_id</c>.</summary>
    public Guid RunId { get; }

    /// <summary>Total samples requested for the run.</summary>
    public int NumSamples { get; }

    /// <summary>Output data rate in Hz — the run's time base.</summary>
    public int OdrHz { get; }

    /// <summary>Accelerometer full-scale range (wire byte).</summary>
    public short AccelRange { get; }

    /// <summary>Gyroscope full-scale range (wire byte).</summary>
    public short GyroRange { get; }

    /// <summary>When management dispatched the <c>START_RUN</c> command (UTC).</summary>
    public DateTimeOffset StartedAtUtc { get; }

    /// <summary>
    /// Validates and builds the parameters from the wire fields. Throws
    /// <see cref="TelemetryValidationException"/> if the GUID/runId are not UUIDs, the ODR is not
    /// positive, or the requested sample count is negative.
    /// </summary>
    public static RunParameters Create(
        string deviceGuid, string runId, long numSamples, int odrHz,
        short accelRange, short gyroRange, DateTimeOffset startedAtUtc)
    {
        if (!Guid.TryParse(deviceGuid, out Guid guid))
        {
            throw new TelemetryValidationException(
                $"Device GUID '{deviceGuid}' is not a valid UUID.");
        }

        if (!Guid.TryParse(runId, out Guid run))
        {
            throw new TelemetryValidationException($"Run id '{runId}' is not a valid UUID.");
        }

        if (odrHz <= 0)
        {
            throw new TelemetryValidationException($"ODR {odrHz} Hz is not positive.");
        }

        if (numSamples < 0)
        {
            throw new TelemetryValidationException($"Requested sample count {numSamples} is negative.");
        }

        return new RunParameters(
            guid, run, (int)numSamples, odrHz, accelRange, gyroRange, startedAtUtc);
    }
}
