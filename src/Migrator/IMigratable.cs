namespace CosmosDb.Migrator;

public interface IMigratable
{
    public string Id { get; }
    public string DocumentType { get; }
    public long? Version { get; set; }
}
