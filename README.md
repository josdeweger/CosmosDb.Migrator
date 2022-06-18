## Introduction 
The CosmosDb Migrator is aimed at developers/teams that need a deterministic way to create, (unit) test and run CosmosDb migrations. 
This tries solving the problem of creating numerous scripts to create/alter document structures and collections. The idea is 
that migrations are defined as C# classes, and tested as such. The migrations can then be run against all
environments (local emulators, test, prod etc.). Migrations aka database changes become part of source control 
this way and change alongside changes made to your code. Important to note is that the consumer of this package is completely
responsible for making sure the CosmosDb Client and collections are prepared for bulk execution, see [Running migrations](#running-migrations).

## Installing the MigrationsRunner
The `Migrator` is built as a NuGet package called `CosmosDb.Migrator` and pushed to nuget.org

## Creating a data migration
To create a data migration definition:
- create a migration class that derives from the `CosmosDbMigration` base class
- mark the class with the `[Migration({version-nr})]` attribute (a handy way to make sure
you don't have colliding versions is to use a `long` resembling a datetime, e.g.: `20220115131600`)
- implement the `Up` and `Down` methods using the methods the library provides
- make sure your document classes are derived from either `Migratable` or, in case of a record, from `MigratableRecord`
This way they automatically get an `id`, `documentType` and `version` field, which are used as part of the migrations

An example:

```c#
[Migration(version: 20220419081800)]
public class TestDataMigration : CosmosDbMigration
{
    public TestDataMigration() : base(collection: "test-data")
    {
    }

    public override void Up()
    {
        OnCollection()
          .WithPartitionKey(path: "/id", key: "id")
          .ForDocumentType("TestDataDocument")
          .Migrate<TestDataDocument, TestDataDocumentV2>(testData => new TestDataDocumentV2(
              Id: testData.Id,
              DocumentType: nameof(TestDataDocumentV2),
              Version: 20220419081800,
              SomeString: testData.SomeString,
              SomeBool: bool.Parse(testData.SomeBoolStr),
              SomeDateTime: DateTime.ParseExact(testData.SomeDateTimeStr, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
    }

    public override void Down()
    {
        OnCollection()
          .WithPartitionKey(path: "/id", key: "id")
          .ForDocumentType("TestDataDocumentV2")
          .Migrate<TestDataDocumentV2, TestDataDocument>(testDataV2 => new TestDataDocument(
              Id: testDataV2.Id.Substring(0, testDataV2.Id.LastIndexOf('-')),
              DocumentType: nameof(TestDataDocument),
              Version: null,
              SomeString: testDataV2.SomeString,
              SomeBoolStr: testDataV2.SomeBool.ToString(),
              SomeDateTimeStr: testDataV2.SomeDateTime.ToString("yyyy-MM-dd")));
    }
}
```

## Creating a collection rename migration
To create a collection rename migration definition:
- derive a class from the `CosmosDbMigration` base class
- mark the class with the `[Migration({version-nr})]` attribute (a handy way to make sure
you don't have colliding versions is to use a `long` resembling a datetime, e.g.: `20220115131600`)
- implement the `Up` and `Down` methods using the methods the library provides

An example:

```c#
[Migration(version: 20220415142200)]
public class ChangeContainerNameTestToTest2 : CosmosDbMigration
{
    public ChangeContainerNameTestToTest2() : base("test")
    {
    }
    
    public override void Up()
    {
        OnCollection()
          .WithPartitionKey("/id", "id")
          .RenameFrom("test")
          .RenameTo("test2");
    }

    public override void Down()
    {
        OnCollection()
          .WithPartitionKey("/id", "id")
          .RenameFrom("test2")
          .RenameTo("test");
    }
}
```

## Running migrations
To actually run the migrations the `Migrator` can be used. It can be used inside any process that can be triggered, for instance
an Azure (Durable) Function, a Console Application etc. Make sure you don't run the `Migrator` inside an application
that might run multiple instances, since these could run the migrations simultaneously, resulting in strange / unwanted behaviour.

You as a developer are responsible for setting up the CosmosDb Client properly to be able to handle bulk execution, and to setup throughput 
on the target collection. See [Setting up the CosmosDb Client](#setting-up-the-cosmosdb-client) and 
[Temporarily setting throughput](#temporarily-setting-throughput) below. 

### Setting up the CosmosDb Client
To ensure fast and reliable migrations, the CosmosDb Client needs to be configured to:
- be allowed to run Bulk Execution
- have the appropriate amount of retries set (the CosmosDb retries throttled requests 9 times by default)

This can be set when instantiating the CosmosDb client, e.g.:

```c#
var cosmosClient =
    new CosmosClient(
        cfg.Value.ConnectionString,
        new CosmosClientOptions
        {
            //when CosmosClientOptions.AllowBulkExecution is enabled, the SDK will make sure
            //all these concurrent point operations will be executed together (that is in bulk)
            //https://docs.microsoft.com/nl-nl/azure/cosmos-db/tutorial-sql-api-dotnet-bulk-import
            AllowBulkExecution = true,
            MaxRetryAttemptsOnRateLimitedRequests = 999,
            //other options
        });
```

### Temporarily setting throughput
When migrating large amounts of data, with low throughput settings (e.g. 400 RU/s) on the collection, a lot of retries will occur. This will 
significantly slow down the migrations. When hitting the max amount of retries the migrations even start failing. Temporarily increase 
the throughput of the collection to a high number of RU/s. The `Migrator` will log every batch it executes, including migrations that 
failed when all retries failed. Make sure you reinstate old throughput settings, otherwise you might receive an unexpected bill at the end of 
the month!

## (Integration) testing your migrations
For local development and testing Microsoft provides [the CosmosDb emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator?tabs=ssl-netstd21).
This can be set up in several ways, for instance by spinning up a Docker container. Once the emulator is up and
running, integration tests can be written against a "real" database. See the `CosmosDbEmulatorFixture` in this
repo to see an example of how a clean database can be set up and seeded to do integration testing.

## Running integration tests in an Azure DevOps pipeline
Windows Azure DevOps agents (since `windows-2019`) have the CosmosDb emulator already installed on the
machine. However, the emulator needs to be started explicitly, and it takes a bit of time to start up so it's 
wise to do this at the start of your pipeline using for instance Powershell:
```
steps:
    - powershell: 'Start-Process -FilePath "C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe" -ArgumentList "/noui"'
      displayName: 'Start CosmosDB emulator'
```

Now you can run the tests against `https://localhost:{port}`.

Check out the `deploy` folder in this repo for an Azure DevOps pipeline example.

## How it works under the hood
### General workings
- The user of the library instantiates the `Migrator` with a set of assemblies to scan for migrations or
with a list of migrations to run
- When performing an `Up` migration:
    - the `Migrator` will look in the collection for a document called `versionDocument`. It will check on 
      what version the collection currently is. If none is found it is seen as version 0
    - all the migration definitions with a version lower or equal to the version in the `versionDocument` will be
      filtered out so only the relevant migrations remain
    - the action that should be performed on the documents in the collection will be run against the documents
      with the `documentType` that is defined in the migration definition. This will be done in batches, the `Migrator`
      will create batches of tasks and run them using `Task.WhenAll`. When Bulk Execution is enabled on the CosmosDb Client 
      (which it should!), the CosmosDb Client will be smart enough to handle these tasks as performant as possible 
    - the new version will be stored with the same partition key, overwriting the existing document
    - when all migrations are finished a `versionDocument` will be created inside the same collection, storing the `documentType` 
      and the current `version`
- When performing a `Down` migration, a version must be added as a parameter to the runner, so it knows to
  what version to downgrade to. Then it will:
    - filter out all the migration definitions that are bigger than the version specified
    - execute the actions defined in the migration definitions `down` method in descending order
    - when all migrations are finished a `versionDocument` will be created inside the same collection, storing the `documentType`
      and the current `version`

### Renaming a collection
Unfortunately CosmosDb does not offer a collection rename method. So when renaming a collection, what actually happens:
- the collection is copied with the same properties but with the new collection name
- all documents in the old collection are copied to the new collection
- the old collection is removed
- a `versionDocument` will be created inside the new collection, storing the `documentType` and the current `version`

## To do
- Support multiple version documents per collection (meaning storing an array of versions in the 
`versionDocument`, one per `documentType`)
- Splitting the `MigrationConfig` into separate configs for different types of migrations (data migrations, renaming collections 
at the moment)