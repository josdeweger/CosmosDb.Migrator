namespace CosmosDb.Migrator;

public abstract class CosmosDbMigration
{
    public MigrationConfig MigrationConfig { get; }

    public CosmosDbMigration(string collectionName)
    {
        MigrationConfig = new MigrationConfig(collectionName);
    }
    
    public abstract void Up();
    public abstract void Down();

    /// <summary>
    /// Start here to define the migration, use the fluent interface to further define the migration configuration
    /// </summary>
    /// <returns>MigrationConfig</returns>
    protected MigrationConfig OnCollection()
    {
        return MigrationConfig;
    }
}
