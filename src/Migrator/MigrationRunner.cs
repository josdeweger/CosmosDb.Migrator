using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CosmosDb.Migrator;

public class MigrationRunner : IMigrationRunner
{
    private const string VersionDocumentPartitionKey = "versionDocument";
    private const int MaxQueryConcurrency = -1;
    private readonly Database _db;
    private readonly JsonSerializer _serializer;
    private readonly ILogger _logger;
    private readonly IProvideCurrentVersion _currentVersionProvider;
    private readonly List<Type> _assemblyMigrations;

    /// <summary>
    /// Create migration runner instance to use for executing CosmosDb migrations up or down from current version
    /// </summary>
    /// <param name="db">CosmosDb database instance</param>
    /// <param name="memoryCache"></param>
    /// <param name="migrationTypes">List of migration types</param>
    /// <param name="logger"></param>
    /// <param name="customSerializer">Custom serializer with preferred serializer settings</param>
    public MigrationRunner(
        Database db,
        ILogger logger, 
        List<Type> migrationTypes, 
        JsonSerializer? customSerializer = null)
    {
        _db = db;
        _logger = logger;
        _currentVersionProvider = new CurrentVersionProvider(db, new MemoryCache(new MemoryCacheOptions()), logger);
        _assemblyMigrations = new List<Type>(migrationTypes);
        _serializer = customSerializer ?? new JsonSerializer();
    }

    /// <summary>
    /// Create migration runner instance to use for executing CosmosDb migrations up or down from current version
    /// </summary>
    /// <param name="db">CosmosDb database instance</param>
    /// <param name="memoryCache"></param>
    /// <param name="assembliesContainingMigrations">List of assemblies that can be scanned for migration types</param>
    /// <param name="customSerializer"></param>
    /// <param name="logger"></param>
    public MigrationRunner(
        Database db, 
        ILogger logger,
        IEnumerable<Assembly> assembliesContainingMigrations, 
        JsonSerializer? customSerializer = null)
        : this(
            db, 
            logger,
            new List<Type>(GetTypesFromAssemblies<CosmosDbMigration>(assembliesContainingMigrations)),
            customSerializer)
    {
    }

    /// <summary>
    /// Loops through all migrations that were added to this MigrationRunner and:
    /// - Gets the current version document from the container that the migration is running on
    /// - If it exists it will use that as current version, otherwise it will start at 0 as current version
    /// - Checks whether the version on the migration is newer than the current version
    /// - If it is the case, it will execute the migration on all documents in the container with the same document type
    /// - When finished with a migration, it will upsert the version document in the same container with the new version from the migration 
    /// </summary>
    public async Task MigrateUp()
    {
        var migrationInfos = CreateMigrationInfoList()
            .OrderBy(x => x.Version)
            .ToList();
        
        //iterate over migrations for this collection
        foreach (var migrationInfo in migrationInfos)
        {
            //execute the Up method to set all the properties on the MigrationConfig property
            migrationInfo.Migration.Up();

            if (migrationInfo.Migration.MigrationConfig is null)
            {
                continue;
            }
            
            //get the current version from the existing collection (if any)
            var currentVersion = await _currentVersionProvider.Get(
                collectionName: migrationInfo.Migration.MigrationConfig.CollectionName,
                partitionKeyPath: migrationInfo.Migration.MigrationConfig.PartitionKeyPath);

            if (migrationInfo.Version <= currentVersion)
            {
                continue;
            }
        
            _logger.LogInformation("Start Up migration (version: {Version}) on collection {CollectionName}", 
                migrationInfo.Version, migrationInfo.Migration.MigrationConfig.CollectionName);

            await RunMigration(migrationInfo.Migration, MigrationDirection.Up, migrationInfo.Version,
                migrationInfo.Version);
        }
    }

    /// <summary>
    /// Loops through all migrations that were added to this MigrationRunner and:
    /// - Gets the current version document from the container that the migration is running on
    /// - If it exists it will use that as current version, otherwise it will start at 0 as current version (and thus do no down migrations at all)
    /// - Checks whether the version on the migration is older than the current version
    /// - If that is the case, it will execute the migration on all documents in the container with the same document type
    /// - When finished with a migration, it will upsert the version document in the same container with the new version from the migration 
    /// </summary>
    public async Task MigrateDown(long toVersion)
    {
        var migrationInfos = CreateMigrationInfoList()
            .Where(x => x.Version >= toVersion)
            .OrderByDescending(x => x.Version)
            .ToList();
        
            //iterate over migrations for this collection
            foreach (var (migrationInfo, index) in migrationInfos.WithIndex().ToList())
            {
                //execute the Down method to set all the properties on the MigrationConfig property
                migrationInfo.Migration.Down();

                if (migrationInfo.Migration.MigrationConfig is null)
                {
                    continue;
                }
                
                _logger.LogInformation("Start Down migration (version: {Version}) on collection {CollectionName}",
                    migrationInfo.Version, migrationInfo.Migration.MigrationConfig.CollectionName);

                var previousVersion = index < migrationInfos.Count - 1
                    ? migrationInfos[index + 1].Version
                    : 0;

                await RunMigration(migrationInfo.Migration, MigrationDirection.Down, migrationInfo.Version,
                    previousVersion);
            }
    }

    private IEnumerable<MigrationInfo> CreateMigrationInfoList()
    {
        return _assemblyMigrations
            .Where(migrationType => migrationType.GetCustomAttribute<MigrationAttribute>() is not null)
            .Select(migrationType => new MigrationInfo(
                migration: (CosmosDbMigration) Activator.CreateInstance(migrationType)!,
                migrationType: migrationType,
                version: migrationType.GetCustomAttribute<MigrationAttribute>()?.Version ??
                         throw new Exception(nameof(MigrationAttribute.Version))));
    }

    private async Task RunMigration(
        CosmosDbMigration migration, 
        MigrationDirection direction, 
        long migrationVersion, 
        long newVersion)
    {
        var migrationTask = migration.MigrationConfig switch
        {
            CollectionRenameConfig config => RunCollectionRename(config, newVersion),
            DataMigrationConfig config => RunDataMigration(config, direction, migrationVersion, newVersion),
            _ => throw new ArgumentOutOfRangeException()
        };

        await migrationTask;
    }

    private async Task RunCollectionRename(CollectionRenameConfig config, long? newVersion)
    {
        _logger.LogInformation("Renaming collection {FromCollection} to {ToCollection} started",
            config.CollectionName, config.ToCollectionName);
        
        var oldContainer = _db.GetContainer(config.CollectionName);
        var response = await oldContainer.ReadContainerAsync();
        var throughput = await oldContainer.ReadThroughputAsync();

        var newContainerProps = response.Resource;
        newContainerProps.Id = config.ToCollectionName;
        
        var newContainerResponse = await _db.CreateContainerIfNotExistsAsync(newContainerProps);
        var newContainer = newContainerResponse.Container;
        await newContainer.ReplaceThroughputAsync(throughput ?? 400);
        
        await CopyAllDataBetweenCollections(oldContainer, newContainer, config.PartitionKey, newVersion);
        await oldContainer.DeleteContainerAsync();
        await UpdateCurrentVersion(newContainer, config.PartitionKey, newVersion);
        
        _logger.LogInformation("Renaming collection {FromCollection} to {ToCollection} finished",
            config.CollectionName, config.ToCollectionName);
    }

    private async Task CopyAllDataBetweenCollections(Container oldContainer, Container newContainer, string partitionKey, long? newVersion)
    {
        var query = new QueryDefinition("select * from c");

        using var iterator = oldContainer.GetItemQueryStreamIterator(query, null, new QueryRequestOptions()
        {
            MaxConcurrency = MaxQueryConcurrency
        });

        while (iterator.HasMoreResults)
        {
            var updateTasks = new List<Task>();
            using var response = await iterator.ReadNextAsync();
            using var sr = new StreamReader(response.Content);
            using var jtr = new JsonTextReader(sr);
            var result = await JObject.LoadAsync(jtr);
            var documentTokens = result.GetValue("Documents");

            foreach (var documentToken in documentTokens)
            {
                var key = documentToken[partitionKey].Value<string>();
                documentToken["version"] = newVersion;

                var task = newContainer.UpsertItemAsync(documentToken, new PartitionKey(key));
                
                updateTasks.Add(task);
            }
            
            _logger.LogInformation("Copied {Count} documents to new collection", updateTasks.Count);

            await Task.WhenAll(updateTasks);
        }
    }

    private async Task RunDataMigration(
        DataMigrationConfig config, 
        MigrationDirection direction, 
        long migrationVersion,
        long newVersion)
    {
        _logger.LogInformation("Data migration with version {Version} started", migrationVersion);
        
        var container = _db.GetContainer(config.CollectionName);
        var query = BuildDataMigrationQuery(config, direction, migrationVersion);

        using var iterator = container.GetItemQueryStreamIterator(query, null, new QueryRequestOptions()
        {
            MaxConcurrency = MaxQueryConcurrency
        });

        while (iterator.HasMoreResults)
        {
            using var response = await iterator.ReadNextAsync();
            using var sr = new StreamReader(response.Content);
            using var jtr = new JsonTextReader(sr);
                
            var result = await JObject.LoadAsync(jtr);
            var documentTokens = result.GetValue("Documents");
            var updateTasks = new List<Task>();
            
            foreach (var documentToken in documentTokens)
            {
                var oldDoc = documentToken.ToObject(config.FromType, _serializer) as IMigratable;
                
                //check if configured conditions are met
                if (!config.AreConditionsMet(container, oldDoc))
                {
                    continue;
                }
                
                dynamic newDoc = await config.Invoke(container, oldDoc);
                
                var partitionKey = newDoc.GetType().GetProperty(config.PartitionKey,
                    BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance).GetValue(newDoc, null);
                
                newDoc.Version = newVersion;

                var task = config.ToType.IsSubclassOf(typeof(Migratable))
                        ? container.UpsertItemAsync<Migratable>(newDoc, new PartitionKey(partitionKey))
                        : container.UpsertItemAsync<MigratableRecord>(newDoc, new PartitionKey(partitionKey));
                
                updateTasks.Add(task);
            }
            
            _logger.LogInformation("Running {NrOfTasks} migration ({Direction}) tasks, migrating {FromType} to {ToType}",
                updateTasks.Count, direction, config.FromType, config.ToType);
        
            var tasks = Task.WhenAll(updateTasks);
            var exceptions = new List<Exception>();
        
            try
            {
                await tasks;
            }
            catch
            {
                if (tasks.Exception is not null)
                {
                    exceptions.AddRange(tasks.Exception.InnerExceptions.ToList());
                }
            }

            if (exceptions.Any())
            {
                _logger.LogError("{Count} exceptions have been thrown while trying to migrate the data", exceptions.Count);
                exceptions.ForEach(e => _logger.LogError("Message: {Msg}", e.Message));
            }
            else
            {
                _logger.LogInformation("{Count} migrations successfully executed", updateTasks.Count);
            }
        }

        await UpdateCurrentVersion(container, config.PartitionKey, newVersion);
        
        _logger.LogInformation("Data migration with version {Version} finished", migrationVersion);
    }

    private QueryDefinition BuildDataMigrationQuery(DataMigrationConfig config, MigrationDirection direction, long newVersion)
    {
        QueryDefinition query;

        var comparison = direction switch
        {
            MigrationDirection.Down => ">=",
            MigrationDirection.Up => "<",
            _ => throw new NotImplementedException(nameof(direction))
        };
        
        if (config.DocumentType.Equals(DataMigrationConfig.EmptyDocumentType))
        {
            query = new QueryDefinition("select * from c " +
                                        "where (not is_defined(c.documentType) or IS_NULL(c.documentType)) " +
                                        $"and (c.version {comparison} @version or not is_defined(c.version) or IS_NULL(c.version))")
                .WithParameter("@version", newVersion);
        }
        else
        {
            query = new QueryDefinition("select * from c " +
                                        "where c.documentType = @documentType " +
                                        $"and (c.version {comparison} @version or not is_defined(c.version) or IS_NULL(c.version))")
                .WithParameter("@documentType", config.DocumentType)
                .WithParameter("@version", newVersion);
        }

        _logger.LogInformation("Build data migration query: {Query}", query.QueryText);
        
        return query;
    }

    private async Task UpdateCurrentVersion(Container container, string partitionKey, long? newVersion)
    {
        var timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        var versionDoc = new VersionDocument(VersionDocumentPartitionKey, nameof(VersionDocument), newVersion);
        var versionDocJObj = JObject.FromObject(versionDoc);
        versionDocJObj[partitionKey] = VersionDocumentPartitionKey;
        await container.UpsertItemAsync(versionDocJObj, new PartitionKey(VersionDocumentPartitionKey));

        _logger.LogInformation("Updated version document in {ContainerName} to version {Version}", container.Id,
            newVersion);
    }
    
    private static List<Type> GetTypesFromAssemblies<T>(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(p => typeof(T).IsAssignableFrom(p) && p.IsPublic && !p.IsAbstract)
            .Distinct()
            .ToList();
    }
}
