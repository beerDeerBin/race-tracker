using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using RaceTracker.Management.Application.Abstractions;
using RaceTracker.Management.Domain.Images;

namespace RaceTracker.Management.Infrastructure.Persistence.Images;

/// <summary>
/// Real GridFS-backed image store (<c>/A30/</c>, anti-stub). Each image is one GridFS file in the
/// <c>vehicleImages</c> bucket, tagged with <c>metadata.vehicleGuid</c> (the verbatim, case-sensitive
/// device GUID) and <c>metadata.contentType</c>. Every read/delete is filtered by <b>both</b> the
/// file id and the vehicle guid, so an image can never be reached through the wrong vehicle. A
/// malformed (non-ObjectId) image id is treated as "not found" rather than throwing.
/// </summary>
public sealed class GridFsVehicleImageStore : IVehicleImageStore
{
    private const string VehicleGuidKey = "vehicleGuid";
    private const string ContentTypeKey = "contentType";

    private readonly IGridFSBucket _bucket;
    private readonly TimeProvider _timeProvider;

    public GridFsVehicleImageStore(IGridFSBucket bucket, TimeProvider timeProvider)
    {
        _bucket = bucket;
        _timeProvider = timeProvider;
    }

    public async Task<VehicleImage> UploadAsync(
        string vehicleGuid,
        string fileName,
        string contentType,
        Stream content,
        long length,
        CancellationToken cancellationToken)
    {
        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { VehicleGuidKey, vehicleGuid },
                { ContentTypeKey, contentType },
            },
        };

        ObjectId id = await _bucket.UploadFromStreamAsync(
            fileName, content, options, cancellationToken);

        return new VehicleImage
        {
            Id = id.ToString(),
            VehicleGuid = vehicleGuid,
            FileName = fileName,
            ContentType = contentType,
            Length = length,
            UploadedAt = _timeProvider.GetUtcNow(),
        };
    }

    public async Task<IReadOnlyList<VehicleImage>> ListAsync(
        string vehicleGuid, CancellationToken cancellationToken)
    {
        FilterDefinition<GridFSFileInfo> filter =
            Builders<GridFSFileInfo>.Filter.Eq($"metadata.{VehicleGuidKey}", vehicleGuid);
        var find = new GridFSFindOptions
        {
            Sort = Builders<GridFSFileInfo>.Sort.Descending(file => file.UploadDateTime),
        };

        using IAsyncCursor<GridFSFileInfo> cursor =
            await _bucket.FindAsync(filter, find, cancellationToken);
        List<GridFSFileInfo> files = await cursor.ToListAsync(cancellationToken);
        return files.Select(ToImage).ToList();
    }

    public async Task<VehicleImageContent?> OpenReadAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken)
    {
        GridFSFileInfo? info = await FindOneAsync(vehicleGuid, imageId, cancellationToken);
        if (info is null)
        {
            return null;
        }

        Stream stream = await _bucket.OpenDownloadStreamAsync(
            info.Id, cancellationToken: cancellationToken);
        return new VehicleImageContent(ToImage(info), stream);
    }

    public async Task<bool> ExistsAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken) =>
        await FindOneAsync(vehicleGuid, imageId, cancellationToken) is not null;

    public async Task<bool> DeleteAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken)
    {
        GridFSFileInfo? info = await FindOneAsync(vehicleGuid, imageId, cancellationToken);
        if (info is null)
        {
            return false;
        }

        await _bucket.DeleteAsync(info.Id, cancellationToken);
        return true;
    }

    private async Task<GridFSFileInfo?> FindOneAsync(
        string vehicleGuid, string imageId, CancellationToken cancellationToken)
    {
        // A non-ObjectId id can never name a stored image — answer "not found" without querying.
        if (!ObjectId.TryParse(imageId, out ObjectId id))
        {
            return null;
        }

        FilterDefinition<GridFSFileInfo> filter = Builders<GridFSFileInfo>.Filter.And(
            Builders<GridFSFileInfo>.Filter.Eq(file => file.Id, id),
            Builders<GridFSFileInfo>.Filter.Eq($"metadata.{VehicleGuidKey}", vehicleGuid));

        using IAsyncCursor<GridFSFileInfo> cursor =
            await _bucket.FindAsync(filter, cancellationToken: cancellationToken);
        return await cursor.FirstOrDefaultAsync(cancellationToken);
    }

    private static VehicleImage ToImage(GridFSFileInfo info) => new()
    {
        Id = info.Id.ToString(),
        VehicleGuid = MetadataString(info, VehicleGuidKey),
        FileName = info.Filename,
        ContentType = MetadataString(info, ContentTypeKey, "application/octet-stream"),
        Length = info.Length,
        UploadedAt = new DateTimeOffset(info.UploadDateTime, TimeSpan.Zero),
    };

    private static string MetadataString(GridFSFileInfo info, string key, string fallback = "")
    {
        BsonValue? value = info.Metadata?.GetValue(key, fallback);
        return value is not null && value.IsString ? value.AsString : fallback;
    }
}
