using Newtonsoft.Json;

namespace CosmosDb.Migrator;

public record MigratableRecord(string Id, string DocumentType, long? Version = null) : IMigratable
{
    [JsonProperty("version")] 
    public long? Version { get; set; } = Version;
    
    [JsonProperty("id")] 
    public string Id { get; set; } = Id;
    
    [JsonProperty("documentType")]
    public string DocumentType { get; set; } = DocumentType;
}
