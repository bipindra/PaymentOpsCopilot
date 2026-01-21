using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;
using PaymentOps.Backend.Infrastructure;
using Xunit;

namespace PaymentOps.Backend.Tests;

public class VectorStoreTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly IConfiguration _configuration;

    public VectorStoreTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<Type>()))
            .Returns(loggerMock.Object);

        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "Qdrant" },
            { "VectorStore:VectorSize", "1536" },
            { "VectorStore:Qdrant:BaseUrl", "http://localhost:6333" },
            { "VectorStore:Qdrant:CollectionName", "test_collection" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }

    [Fact]
    public void VectorStoreFactory_CreatesQdrantStore_WhenProviderIsQdrant()
    {
        // Act
        var store = VectorStoreFactory.Create(_configuration, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(store);
        Assert.IsType<QdrantVectorStore>(store);
    }

    [Fact]
    public void VectorStoreFactory_ThrowsException_WhenProviderIsUnknown()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "UnknownProvider" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            VectorStoreFactory.Create(config, _loggerFactoryMock.Object));
    }

    [Fact]
    public void VectorStoreFactory_CreatesAzureAISearchStore_WhenProviderIsAzureAISearch()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "AzureAISearch" },
            { "VectorStore:VectorSize", "1536" },
            { "VectorStore:AzureAISearch:ServiceName", "test-service" },
            { "VectorStore:AzureAISearch:IndexName", "test-index" },
            { "VectorStore:AzureAISearch:ApiKey", "test-key" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var store = VectorStoreFactory.Create(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(store);
        Assert.IsType<AzureAISearchVectorStore>(store);
    }

    [Fact]
    public void VectorStoreFactory_CreatesPostgresStore_WhenProviderIsPostgres()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "Postgres" },
            { "VectorStore:VectorSize", "1536" },
            { "VectorStore:Postgres:ConnectionString", "Host=localhost;Database=test" },
            { "VectorStore:Postgres:TableName", "chunks" },
            { "VectorStore:Postgres:SchemaName", "vector" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var store = VectorStoreFactory.Create(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(store);
        Assert.IsType<PostgresPgVectorStore>(store);
    }

    [Fact]
    public void VectorStoreFactory_CreatesRedisStore_WhenProviderIsRedis()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "Redis" },
            { "VectorStore:VectorSize", "1536" },
            { "VectorStore:Redis:ConnectionString", "localhost:6379" },
            { "VectorStore:Redis:IndexName", "idx:chunks" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var store = VectorStoreFactory.Create(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(store);
        Assert.IsType<RedisVectorStore>(store);
    }

    [Fact]
    public void VectorStoreFactory_CreatesOpenSearchStore_WhenProviderIsOpenSearch()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "VectorStore:Provider", "OpenSearch" },
            { "VectorStore:VectorSize", "1536" },
            { "VectorStore:OpenSearch:Uri", "https://localhost:9200" },
            { "VectorStore:OpenSearch:IndexName", "test-index" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var store = VectorStoreFactory.Create(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(store);
        Assert.IsType<OpenSearchVectorStore>(store);
    }
}
