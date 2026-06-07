using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Application.Discovery;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Vehicles;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the device-discovery use case (story 5.4) with its persistence ports mocked. Proves
/// that an unknown GUID becomes a lazily-created <c>pending</c> vehicle — with the GUID stored
/// <b>verbatim</b> (case preserved), unowned, status <c>pending</c> — and that a GUID already known
/// (pending <i>or</i> claimed) is left untouched, so a late status event never downgrades a vehicle.
/// </summary>
public sealed class DeviceDiscoveryServiceTests : IDisposable
{
    // A deliberately MIXED-case GUID: discovery must store it unchanged (case-sensitive correlation key).
    private const string MixedCaseGuid = "AbCdEf12-3456-7890-ABCD-1234567890Ef";

    private readonly IRepository<Vehicle> _repository = Substitute.For<IRepository<Vehicle>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ManagementMetrics _metrics = new();

    public void Dispose() => _metrics.Dispose();

    private DeviceDiscoveryService CreateSut() => new(
        new CrudService<Vehicle>(_repository, _unitOfWork, _metrics),
        _metrics,
        TimeProvider.System,
        NullLogger<DeviceDiscoveryService>.Instance);

    [Fact]
    public async Task Unknown_guid_is_lazily_registered_as_a_pending_vehicle()
    {
        // The repository returns null by default → the GUID is unknown.
        Vehicle? added = null;
        _repository.When(r => r.Add(Arg.Any<Vehicle>())).Do(call => added = call.Arg<Vehicle>());
        DateTimeOffset before = DateTimeOffset.UtcNow;

        long discovered = CountDiscovered(out MeterListener listener);
        bool created;
        using (listener)
        {
            created = await CreateSut().EnsurePendingAsync(MixedCaseGuid, CancellationToken.None);
        }

        created.ShouldBeTrue();
        _repository.Received(1).Add(Arg.Any<Vehicle>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        added.ShouldNotBeNull();
        added!.Id.ShouldBe(MixedCaseGuid); // verbatim — never lower-cased.
        added.Name.ShouldBe(MixedCaseGuid); // sensible default display name until claimed.
        added.Owner.ShouldBe(""); // unowned until claimed.
        added.RegistrationStatus.ShouldBe(RegistrationStatus.Pending);
        added.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);

        ReadDiscovered().ShouldBe(discovered + 1);
    }

    [Fact]
    public async Task Known_guid_is_left_untouched_and_not_re_discovered()
    {
        // A vehicle already exists for this GUID (e.g. already claimed → registered).
        _repository.GetByIdAsync(MixedCaseGuid, Arg.Any<CancellationToken>()).Returns(new Vehicle
        {
            Id = MixedCaseGuid,
            Name = "Race Truck",
            Owner = "admin",
            RegistrationStatus = RegistrationStatus.Registered,
            CreatedAt = DateTimeOffset.UnixEpoch,
        });

        long discovered = CountDiscovered(out MeterListener listener);
        bool created;
        using (listener)
        {
            created = await CreateSut().EnsurePendingAsync(MixedCaseGuid, CancellationToken.None);
        }

        created.ShouldBeFalse();
        _repository.DidNotReceive().Add(Arg.Any<Vehicle>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        ReadDiscovered().ShouldBe(discovered); // no new discovery recorded.
    }

    // Listens to the discovery counter so the §8-observability requirement is actually asserted.
    private long _discoveredTotal;

    private long CountDiscovered(out MeterListener listener)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ManagementMetrics.MeterName &&
                    instrument.Name == "racetracker_management_devices_discovered_total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => _discoveredTotal += measurement);
        listener.Start();
        return _discoveredTotal;
    }

    private long ReadDiscovered() => _discoveredTotal;
}
