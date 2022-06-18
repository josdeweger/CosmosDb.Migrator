namespace CosmosDb.Migrator;

public class MigrationInfo
{
    public CosmosDbMigration Migration { get; }
    public Type MigrationType { get; }
    public long Version { get; }

    public MigrationInfo(CosmosDbMigration migration, Type migrationType, long version)
    {
        Migration = migration;
        MigrationType = migrationType;
        Version = version;
    }
}
