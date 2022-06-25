using System.Net;
using CosmosDb.Migrator;
using FluentAssertions;
using MemoryCache.Testing.Moq;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace Migrator.UnitTests;

public class CurrentVersionProviderTests
{
    private readonly Mock<Container> _containerMock = new();
    private readonly Mock<Database> _dbMock = new();
    private const string CollectionName = "test";
    private const string PartitionKeyPath = "/id";
    private readonly Mock<ILogger> _logger = new();
    
    [Fact]
    public async Task GivenVersionIsStoredInCache_WhenGettingCurrentVersion_ItReturnsFromCache()
    {
        var currentVersion = 12345L;
        var memoryCacheMock = Create.MockedMemoryCache();
        memoryCacheMock.GetOrCreate(CacheKeys.GetVersionDocumentCacheKey(CollectionName), _ => currentVersion);
        
        var currentVersionProvider = CreateSut(memoryCacheMock);
        var result = await currentVersionProvider.Get(CollectionName, PartitionKeyPath);

        result.Should().Be(currentVersion);
        
        var cacheMock = Mock.Get(memoryCacheMock);
        object cacheEntryValue;
        
        cacheMock.Verify(x => x.TryGetValue(CacheKeys.GetVersionDocumentCacheKey(CollectionName), out cacheEntryValue),
            Times.AtLeast(1));
    }
    
    [Fact]
    public async Task GivenVersionIsNotStoredInCache_AndVersionDocInDb_WhenGettingCurrentVersion_ItIsStoreInCache()
    {
        var versionDoc = new VersionDocument("version-doc-id", nameof(VersionDocument), 12345L);
        var memoryCacheMock = Create.MockedMemoryCache();
        var itemResponseMock = new Mock<ItemResponse<VersionDocument>>();
        itemResponseMock.Setup(i => i.Resource).Returns(versionDoc);

        _containerMock.Setup(x =>
            x.ReadItemAsync<VersionDocument>(
                It.IsAny<string>(), 
                It.IsAny<PartitionKey>(), 
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemResponseMock.Object);
        
        var containerResponseMock = new Mock<ContainerResponse>();
        containerResponseMock.SetupGet(m => m.Container).Returns(_containerMock.Object);

        _dbMock.Setup(m => m.CreateContainerIfNotExistsAsync(It.IsAny<ContainerProperties>(),
                It.IsAny<int?>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerResponseMock.Object);
        
        var currentVersionProvider = CreateSut(memoryCacheMock);
        var result = await currentVersionProvider.Get(CollectionName, PartitionKeyPath);

        result.Should().Be(versionDoc.Version);
        
        var cacheMock = Mock.Get(memoryCacheMock);
        cacheMock.Verify(x => x.CreateEntry(CacheKeys.GetVersionDocumentCacheKey(CollectionName)), Times.Once);
    }
    
    [Fact]
    public async Task GivenVersionIsNotStoredInCache_AndVersionDocNotInDb_WhenGettingCurrentVersion_ItIsStoreInCache()
    {
        var memoryCacheMock = Create.MockedMemoryCache();

        _containerMock.Setup(x =>
                x.ReadItemAsync<VersionDocument>(
                    It.IsAny<string>(), 
                    It.IsAny<PartitionKey>(), 
                    null,
                    It.IsAny<CancellationToken>()))
            .Throws(new CosmosException("Not found", HttpStatusCode.NotFound, 0, string.Empty, 0));
        
        var containerResponseMock = new Mock<ContainerResponse>();
        containerResponseMock.SetupGet(m => m.Container).Returns(_containerMock.Object);

        _dbMock.Setup(m => m.CreateContainerIfNotExistsAsync(It.IsAny<ContainerProperties>(),
                It.IsAny<int?>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(containerResponseMock.Object);
        
        var currentVersionProvider = CreateSut(memoryCacheMock);
        var result = await currentVersionProvider.Get(CollectionName, PartitionKeyPath);

        result.Should().Be(0L);
        
        var cacheMock = Mock.Get(memoryCacheMock);
        cacheMock.Verify(x => x.CreateEntry(CacheKeys.GetVersionDocumentCacheKey(CollectionName)), Times.Once);
    }

    private CurrentVersionProvider CreateSut(IMemoryCache memoryCacheMock)
    {
        return new CurrentVersionProvider(_dbMock.Object, memoryCacheMock, _logger.Object);
    }
}
