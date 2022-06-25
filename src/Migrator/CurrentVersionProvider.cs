using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CosmosDb.Migrator;

public class CurrentVersionProvider : IProvideCurrentVersion
{
    private const string VersionDocumentPartitionKey = "versionDocument";
    private readonly Database _db;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;
    
    public CurrentVersionProvider(Database db, IMemoryCache memoryCache, ILogger logger)
    {
        _db = db;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current version for this collection, by retrieving the versionDocument from the collection. When no
    /// versionDocument is found the current version is set to 0. The current version can be cached in memory
    /// optionally for performance reasons. Cache key will be cleared when the versionDocument is updated
    /// </summary>
    /// <param name="collectionName"></param>
    /// <param name="partitionKeyPath"></param>
    /// <param name="cacheDurationInSecs"></param>
    /// <returns></returns>
    public async Task<long> Get(string collectionName, string partitionKeyPath, int cacheDurationInSecs = 30)
    {
        ContainerProperties props = new(collectionName, partitionKeyPath);
        var containerResponse = await _db.CreateContainerIfNotExistsAsync(props);
        var cacheKey = CacheKeys.GetVersionDocumentCacheKey(collectionName);
        var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheDurationInSecs));

        if (_memoryCache.TryGetValue(cacheKey, out long cachedVersion))
        {
            return cachedVersion;
        }
        
        try
        {
            var versionDocResponse =
                await containerResponse.Container.ReadItemAsync<VersionDocument>(VersionDocumentPartitionKey,
                    new PartitionKey(VersionDocumentPartitionKey));

            var currentVersion = versionDocResponse.Resource?.Version ?? 0;
            _memoryCache.Set(cacheKey, currentVersion, cacheEntryOptions);
            
            return currentVersion;
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Current version {Version} on collection {CollectionName}", 0, collectionName);
                _memoryCache.Set<long>(cacheKey, 0, cacheEntryOptions);
                
                return 0;
            }

            throw;
        }
    }
}
