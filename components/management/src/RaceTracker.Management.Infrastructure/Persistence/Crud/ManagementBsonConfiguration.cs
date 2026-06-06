using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using RaceTracker.Management.Domain.Vehicles;

namespace RaceTracker.Management.Infrastructure.Persistence.Crud;

/// <summary>
/// Registers the BSON class maps so the generic <see cref="MongoRepository{T}"/> can persist the
/// <b>domain entities directly</b> — that is what keeps the repository generic (no hand-written
/// per-type document). The Domain stays attribute-free: the mapping (camelCase field names, the
/// string <c>Id</c> as <c>_id</c>, the enum as its string name) lives here in Infrastructure.
/// Registration is global and process-wide, so it is idempotent and run once at startup.
/// </summary>
public static class ManagementBsonConfiguration
{
    private static readonly object _gate = new();
    private static bool _registered;

    public static void Register()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            // camelCase element names + tolerate fields the entity no longer maps.
            ConventionRegistry.Register(
                "race-tracker-management",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true),
                },
                _ => true);

            if (!BsonClassMap.IsClassMapRegistered(typeof(Vehicle)))
            {
                BsonClassMap.RegisterClassMap<Vehicle>(map =>
                {
                    map.AutoMap();
                    // Vehicle.Id IS the verbatim device GUID → stored as the string _id.
                    map.MapIdMember(v => v.Id);
                    // Persist the lifecycle as a readable string rather than an int.
                    map.MapMember(v => v.RegistrationStatus)
                        .SetSerializer(new EnumSerializer<RegistrationStatus>(BsonType.String));
                });
            }

            _registered = true;
        }
    }
}
