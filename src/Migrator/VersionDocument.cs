using Newtonsoft.Json;

namespace CosmosDb.Migrator;

public record VersionDocument(
    [property: JsonProperty("id")] string Id, 
    [property: JsonProperty("documentType")] string DocumentType,  
    long? Version) : IMigratable
{
    [JsonProperty("version")] public long? Version { get; set; } = Version;
    [JsonProperty("_ts")] public long _ts { get; set; }
}
