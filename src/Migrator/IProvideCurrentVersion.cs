namespace CosmosDb.Migrator;

public interface IProvideCurrentVersion
{
    Task<long?> Get(string collectionName, string partitionKeyPath, int cacheDurationInSecs = 30);
}
