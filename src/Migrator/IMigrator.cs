namespace CosmosDb.Migrator;

public interface IMigrator
{
    Task MigrateUp();
    Task MigrateDown(long toVersion);
}
