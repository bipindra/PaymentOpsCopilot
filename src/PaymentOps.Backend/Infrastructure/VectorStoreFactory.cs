using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Factory for creating the appropriate IVectorStore implementation based on configuration.
/// </summary>
public static class VectorStoreFactory
{
    /// <summary>
    /// Creates an IVectorStore instance based on the configured provider.
    /// </summary>
    public static IVectorStore Create(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var provider = configuration["VectorStore:Provider"] ?? "Qdrant";
        var vectorSize = configuration.GetValue<int>("VectorStore:VectorSize", 1536);
        
        var logger = loggerFactory.CreateLogger($"VectorStoreFactory");

        logger.LogInformation("Creating vector store with provider: {Provider}", provider);

        return provider.ToLowerInvariant() switch
        {
            "qdrant" => CreateQdrant(configuration, vectorSize, loggerFactory),
            "azureaisearch" or "azure-ai-search" => CreateAzureAISearch(configuration, vectorSize, loggerFactory),
            "postgres" or "postgresql" => CreatePostgres(configuration, vectorSize, loggerFactory),
            "redis" => CreateRedis(configuration, vectorSize, loggerFactory),
            "opensearch" => CreateOpenSearch(configuration, vectorSize, loggerFactory),
            _ => throw new InvalidOperationException($"Unknown vector store provider: {provider}. Supported providers: Qdrant, AzureAISearch, Postgres, Redis, OpenSearch")
        };
    }

    private static IVectorStore CreateQdrant(IConfiguration configuration, int vectorSize, ILoggerFactory loggerFactory)
    {
        var baseUrl = configuration["VectorStore:Qdrant:BaseUrl"] 
            ?? configuration["Qdrant:BaseUrl"] 
            ?? "http://localhost:6333";
        var collectionName = configuration["VectorStore:Qdrant:CollectionName"] 
            ?? configuration["Qdrant:CollectionName"] 
            ?? "paymentops_chunks";
        
        var logger = loggerFactory.CreateLogger<QdrantVectorStore>();
        return new QdrantVectorStore(baseUrl, collectionName, vectorSize, logger);
    }

    private static IVectorStore CreateAzureAISearch(IConfiguration configuration, int vectorSize, ILoggerFactory loggerFactory)
    {
        var serviceName = configuration["VectorStore:AzureAISearch:ServiceName"] 
            ?? throw new InvalidOperationException("VectorStore:AzureAISearch:ServiceName is required");
        var indexName = configuration["VectorStore:AzureAISearch:IndexName"] ?? "paymentops-chunks";
        var apiKey = configuration["VectorStore:AzureAISearch:ApiKey"] 
            ?? throw new InvalidOperationException("VectorStore:AzureAISearch:ApiKey is required");
        
        var logger = loggerFactory.CreateLogger<AzureAISearchVectorStore>();
        return new AzureAISearchVectorStore(serviceName, indexName, apiKey, vectorSize, logger);
    }

    private static IVectorStore CreatePostgres(IConfiguration configuration, int vectorSize, ILoggerFactory loggerFactory)
    {
        var connectionString = configuration["VectorStore:Postgres:ConnectionString"] 
            ?? throw new InvalidOperationException("VectorStore:Postgres:ConnectionString is required");
        var tableName = configuration["VectorStore:Postgres:TableName"] ?? "chunks";
        var schemaName = configuration["VectorStore:Postgres:SchemaName"] ?? "vector";
        
        var logger = loggerFactory.CreateLogger<PostgresPgVectorStore>();
        return new PostgresPgVectorStore(connectionString, tableName, schemaName, vectorSize, logger);
    }

    private static IVectorStore CreateRedis(IConfiguration configuration, int vectorSize, ILoggerFactory loggerFactory)
    {
        var connectionString = configuration["VectorStore:Redis:ConnectionString"] 
            ?? throw new InvalidOperationException("VectorStore:Redis:ConnectionString is required");
        var indexName = configuration["VectorStore:Redis:IndexName"] ?? "idx:chunks";
        
        var logger = loggerFactory.CreateLogger<RedisVectorStore>();
        return new RedisVectorStore(connectionString, indexName, vectorSize, logger);
    }

    private static IVectorStore CreateOpenSearch(IConfiguration configuration, int vectorSize, ILoggerFactory loggerFactory)
    {
        var uri = configuration["VectorStore:OpenSearch:Uri"] 
            ?? throw new InvalidOperationException("VectorStore:OpenSearch:Uri is required");
        var indexName = configuration["VectorStore:OpenSearch:IndexName"] ?? "paymentops-chunks";
        var username = configuration["VectorStore:OpenSearch:Username"];
        var password = configuration["VectorStore:OpenSearch:Password"];
        
        var logger = loggerFactory.CreateLogger<OpenSearchVectorStore>();
        return new OpenSearchVectorStore(uri, indexName, username, password, vectorSize, logger);
    }
}
