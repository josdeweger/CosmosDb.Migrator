using Newtonsoft.Json;

namespace CosmosDb.Migrator;

public class Migratable : IMigratable
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;
    
    [JsonProperty("documentType")]
    public string DocumentType { get; set; } = default!;
    
    [JsonProperty("version")]
    public long? Version { get; set; }
}
