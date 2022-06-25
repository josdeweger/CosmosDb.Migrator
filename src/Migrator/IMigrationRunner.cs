namespace CosmosDb.Migrator;

public interface IMigrationRunner
{
    Task MigrateUp();
    Task MigrateDown(long toVersion);
}
