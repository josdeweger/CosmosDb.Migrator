namespace CosmosDb.Migrator.Tests.Migrations;

[Migration(version: 20220415142200)]
public class ChangeContainerNameTestToTest2 : CosmosDbMigration
{
    public ChangeContainerNameTestToTest2() : base("test")
    {
    }
    
    public override void Up()
    {
        OnCollection()
            .RenameFrom("test")
            .WithPartitionKey("/id", "id")
            .RenameTo("test2");
    }

    public override void Down()
    {
        OnCollection()
            .RenameFrom("test2")
            .WithPartitionKey("/id", "id")
            .RenameTo("test");
    }
}
