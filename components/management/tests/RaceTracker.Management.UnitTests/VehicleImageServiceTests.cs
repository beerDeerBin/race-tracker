using Microsoft.Extensions.Options;
using NSubstitute;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Application.Configuration;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Application.Images;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Images;
using RaceTracker.Management.Domain.Vehicles;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the gallery use case with its ports mocked. The image store is a mock; the title
/// bookkeeping runs through a <b>real</b> <see cref="CrudService{T}"/> over a mocked
/// <see cref="IRepository{T}"/> (mirroring <see cref="CrudServiceTests"/>). Proves the validation
/// rules, first-upload auto-title (and that it never overwrites an existing choice), set-title's
/// existence check, and that deleting the title image clears it.
/// </summary>
public sealed class VehicleImageServiceTests : IDisposable
{
    private const string Guid = "AbCdEf12-VEHICLE";

    private readonly IVehicleImageStore _store = Substitute.For<IVehicleImageStore>();
    private readonly IRepository<Vehicle> _vehicles = Substitute.For<IRepository<Vehicle>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ManagementMetrics _metrics = new();

    public void Dispose() => _metrics.Dispose();

    private VehicleImageService CreateSut()
    {
        var crud = new CrudService<Vehicle>(_vehicles, _unitOfWork, _metrics);
        return new VehicleImageService(_store, crud, Options.Create(new ManagementOptions()));
    }

    private static Vehicle AVehicle(string? titleImageId = null) => new()
    {
        Id = Guid,
        Name = "kart-1",
        Owner = "admin",
        RegistrationStatus = RegistrationStatus.Registered,
        CreatedAt = DateTimeOffset.UnixEpoch,
        TitleImageId = titleImageId,
    };

    private static VehicleImage AnImage(string id = "img-1") => new()
    {
        Id = id,
        VehicleGuid = Guid,
        FileName = "kart.png",
        ContentType = "image/png",
        Length = 3,
        UploadedAt = DateTimeOffset.UnixEpoch,
    };

    private Task<VehicleImage> UploadPngAsync(VehicleImageService sut, long length = 3) =>
        sut.UploadAsync(Guid, "kart.png", "image/png", new MemoryStream([1, 2, 3]), length, CancellationToken.None);

    [Fact]
    public async Task Upload_rejects_a_disallowed_content_type_without_storing()
    {
        await Should.ThrowAsync<UnsupportedImageTypeException>(() => CreateSut().UploadAsync(
            Guid, "evil.svg", "image/svg+xml", new MemoryStream([1]), 1, CancellationToken.None));

        await _store.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_rejects_a_file_over_the_size_cap_without_storing()
    {
        long tooBig = new ManagementOptions().Images.MaxBytes + 1;

        await Should.ThrowAsync<ImageTooLargeException>(() => UploadPngAsync(CreateSut(), tooBig));

        await _store.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task First_upload_becomes_the_title_image()
    {
        _store.UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(AnImage("img-1"));
        Vehicle vehicle = AVehicle(titleImageId: null);
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        await UploadPngAsync(CreateSut());

        vehicle.TitleImageId.ShouldBe("img-1");
        _vehicles.Received(1).Update(vehicle);
    }

    [Fact]
    public async Task Upload_does_not_overwrite_an_existing_title_image()
    {
        _store.UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Stream>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(AnImage("img-2"));
        Vehicle vehicle = AVehicle(titleImageId: "img-1");
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        await UploadPngAsync(CreateSut());

        vehicle.TitleImageId.ShouldBe("img-1");
    }

    [Fact]
    public async Task SetTitle_requires_the_image_to_exist_for_the_vehicle()
    {
        _store.ExistsAsync(Guid, "ghost", Arg.Any<CancellationToken>()).Returns(false);

        bool set = await CreateSut().SetTitleAsync(Guid, "ghost", CancellationToken.None);

        set.ShouldBeFalse();
        _vehicles.DidNotReceive().Update(Arg.Any<Vehicle>());
    }

    [Fact]
    public async Task SetTitle_updates_the_vehicle_when_the_image_exists()
    {
        _store.ExistsAsync(Guid, "img-2", Arg.Any<CancellationToken>()).Returns(true);
        Vehicle vehicle = AVehicle(titleImageId: "img-1");
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        bool set = await CreateSut().SetTitleAsync(Guid, "img-2", CancellationToken.None);

        set.ShouldBeTrue();
        vehicle.TitleImageId.ShouldBe("img-2");
    }

    [Fact]
    public async Task Delete_of_the_title_image_promotes_the_next_remaining_image()
    {
        _store.DeleteAsync(Guid, "img-1", Arg.Any<CancellationToken>()).Returns(true);
        // After the delete, img-2 is the only image left (store lists newest-first).
        _store.ListAsync(Guid, Arg.Any<CancellationToken>()).Returns([AnImage("img-2")]);
        Vehicle vehicle = AVehicle(titleImageId: "img-1");
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        bool deleted = await CreateSut().DeleteAsync(Guid, "img-1", CancellationToken.None);

        deleted.ShouldBeTrue();
        vehicle.TitleImageId.ShouldBe("img-2");
    }

    [Fact]
    public async Task Delete_of_the_last_title_image_clears_the_title()
    {
        _store.DeleteAsync(Guid, "img-1", Arg.Any<CancellationToken>()).Returns(true);
        _store.ListAsync(Guid, Arg.Any<CancellationToken>()).Returns([]);
        Vehicle vehicle = AVehicle(titleImageId: "img-1");
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        bool deleted = await CreateSut().DeleteAsync(Guid, "img-1", CancellationToken.None);

        deleted.ShouldBeTrue();
        vehicle.TitleImageId.ShouldBeNull();
    }

    [Fact]
    public async Task Delete_of_a_non_title_image_leaves_the_title()
    {
        _store.DeleteAsync(Guid, "img-2", Arg.Any<CancellationToken>()).Returns(true);
        Vehicle vehicle = AVehicle(titleImageId: "img-1");
        _vehicles.GetByIdAsync(Guid, Arg.Any<CancellationToken>()).Returns(vehicle);

        await CreateSut().DeleteAsync(Guid, "img-2", CancellationToken.None);

        vehicle.TitleImageId.ShouldBe("img-1");
    }

    [Fact]
    public async Task Delete_of_a_missing_image_returns_false_without_touching_the_vehicle()
    {
        _store.DeleteAsync(Guid, "ghost", Arg.Any<CancellationToken>()).Returns(false);

        bool deleted = await CreateSut().DeleteAsync(Guid, "ghost", CancellationToken.None);

        deleted.ShouldBeFalse();
        _vehicles.DidNotReceive().Update(Arg.Any<Vehicle>());
    }
}
