namespace CosmosDb.Migrator;

public abstract class CosmosDbMigration
{
    public MigrationConfig? MigrationConfig { get; private set; }

    public abstract void Up();
    public abstract void Down();

    /// <summary>
    /// Start here to define the data migration, use the fluent interface to further define the migration configuration
    /// </summary>
    /// <returns>DataMigrationConfig</returns>
    protected void MigrateDataInCollection(Func<DataMigrationConfigBuilder, DataMigrationConfig> func)
    {
        MigrationConfig = func(new DataMigrationConfigBuilder());
    }
    
    /// <summary>
    /// Start here to define the collection modification, use the fluent interface to further define the migration configuration
    /// </summary>
    /// <returns>CollectionModificationConfig</returns>
    protected void RenameCollection(Func<CollectionRenameConfigBuilder, CollectionRenameConfig> func)
    {
        MigrationConfig = func(new CollectionRenameConfigBuilder());
    }
}
