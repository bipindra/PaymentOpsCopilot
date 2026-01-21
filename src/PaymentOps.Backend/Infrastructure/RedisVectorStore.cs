using StackExchange.Redis;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;
using System.Text.Json;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Redis Stack with RediSearch implementation of <see cref="IVectorStore"/>.
/// </summary>
public class RedisVectorStore : IVectorStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly string _indexName;
    private readonly int _vectorSize;
    private readonly ILogger<RedisVectorStore> _logger;

    public RedisVectorStore(
        string connectionString,
        string indexName,
        int vectorSize,
        ILogger<RedisVectorStore> logger)
    {
        _indexName = indexName;
        _vectorSize = vectorSize;
        _logger = logger;
        _redis = ConnectionMultiplexer.Connect(connectionString);
        _database = _redis.GetDatabase();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        
        // Check if index exists
        try
        {
            var indexInfo = await server.ExecuteAsync("FT.INFO", _indexName);
            if (indexInfo != null)
            {
                _logger.LogInformation("Index {IndexName} already exists", _indexName);
                return;
            }
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown index name"))
        {
            // Index doesn't exist, create it
        }

        // Create RediSearch index with vector field
        // Using HNSW algorithm for vector search
        var createIndexArgs = new object[]
        {
            "FT.CREATE",
            _indexName,
            "ON", "HASH",
            "PREFIX", "1", "chunk:",
            "SCHEMA",
            "id", "TEXT", "NOSTEM",
            "docId", "TEXT", "NOSTEM",
            "docName", "TEXT",
            "sourcePath", "TEXT",
            "hash", "TEXT", "NOSTEM",
            "chunkIndex", "NUMERIC",
            "text", "TEXT",
            "snippet", "TEXT",
            "embedding", "VECTOR", "FLAT", "6",
                "TYPE", "FLOAT32",
                "DIM", _vectorSize,
                "DISTANCE_METRIC", "COSINE",
            "createdUtc", "NUMERIC", "SORTABLE"
        };

        await server.ExecuteAsync("FT.CREATE", createIndexArgs);
        _logger.LogInformation("Created index {IndexName}", _indexName);
    }

    public async Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (chunksList.Count == 0) return;

        _logger.LogDebug("Upserting {Count} chunks to Redis", chunksList.Count);

        var batch = _database.CreateBatch();
        var tasks = new List<Task>();

        foreach (var chunk in chunksList)
        {
            if (chunk.Embedding == null)
            {
                throw new InvalidOperationException("Chunk must have embedding");
            }

            var key = $"chunk:{chunk.Id}";
            var hashFields = new HashEntry[]
            {
                new("id", chunk.Id.ToString()),
                new("docId", chunk.DocumentId.ToString()),
                new("docName", chunk.DocumentName),
                new("sourcePath", string.Empty),
                new("hash", chunk.Hash),
                new("chunkIndex", chunk.Index),
                new("text", chunk.Text),
                new("snippet", chunk.Snippet),
                new("embedding", ConvertEmbeddingToBytes(chunk.Embedding)),
                new("createdUtc", chunk.CreatedUtc.Ticks)
            };

            tasks.Add(batch.HashSetAsync(key, hashFields));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        _logger.LogInformation("Upserted {Count} chunks", chunksList.Count);
    }

    public async Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        
        // Convert query vector to bytes
        var queryVectorBytes = ConvertEmbeddingToBytes(queryVector);
        var queryVectorBase64 = Convert.ToBase64String(queryVectorBytes);

        // RediSearch vector search query
        // Using KNN search with cosine distance
        var searchArgs = new object[]
        {
            "FT.SEARCH",
            _indexName,
            $"*=>[KNN {topK} @embedding $vec]",
            "PARAMS", "2", "vec", queryVectorBase64,
            "RETURN", "10", "id", "docId", "docName", "sourcePath", "hash", "chunkIndex", "text", "snippet", "createdUtc",
            "SORTBY", "__embedding_score",
            "LIMIT", "0", topK.ToString()
        };

        var result = await server.ExecuteAsync("FT.SEARCH", searchArgs);
        var chunks = ParseSearchResults(result, minScore);

        return chunks;
    }

    public async Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        
        // Search all chunks and aggregate by document
        var searchArgs = new object[]
        {
            "FT.SEARCH",
            _indexName,
            "*",
            "RETURN", "3", "docId", "docName", "sourcePath", "createdUtc",
            "LIMIT", "0", "10000"
        };

        var result = await server.ExecuteAsync("FT.SEARCH", searchArgs);
        var docGroups = new Dictionary<Guid, Document>();

        if (result is RedisResult[] results && results.Length > 1)
        {
            var count = (int)results[0];
            for (int i = 1; i < results.Length; i += 2)
            {
                if (i + 1 < results.Length && results[i + 1] is RedisResult[] fields)
                {
                    var docIdStr = GetFieldValue(fields, "docId");
                    if (Guid.TryParse(docIdStr, out var docId))
                    {
                        if (!docGroups.ContainsKey(docId))
                        {
                            docGroups[docId] = new Document
                            {
                                Id = docId,
                                Name = GetFieldValue(fields, "docName"),
                                SourcePath = GetFieldValue(fields, "sourcePath"),
                                CreatedUtc = long.TryParse(GetFieldValue(fields, "createdUtc"), out var ticks) 
                                    ? new DateTime(ticks) 
                                    : DateTime.UtcNow,
                                ChunkCount = 0,
                                TotalSizeBytes = 0
                            };
                        }
                        docGroups[docId].ChunkCount++;
                        var text = GetFieldValue(fields, "text");
                        docGroups[docId].TotalSizeBytes += text?.Length ?? 0;
                    }
                }
            }
        }

        return docGroups.Values.ToList();
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var documents = await GetDocumentsAsync(cancellationToken);
        return documents.FirstOrDefault(d => d.Id == documentId);
    }

    public async Task<List<Chunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        
        var searchArgs = new object[]
        {
            "FT.SEARCH",
            _indexName,
            $"@docId:{{{documentId}}}",
            "RETURN", "9", "id", "docId", "docName", "sourcePath", "hash", "chunkIndex", "text", "snippet", "createdUtc",
            "SORTBY", "chunkIndex",
            "LIMIT", "0", "10000"
        };

        var result = await server.ExecuteAsync("FT.SEARCH", searchArgs);
        return ParseSearchResults(result, null);
    }

    private byte[] ConvertEmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private List<Chunk> ParseSearchResults(RedisResult result, float? minScore)
    {
        var chunks = new List<Chunk>();

        if (result is RedisResult[] results && results.Length > 1)
        {
            var count = (int)results[0];
            for (int i = 1; i < results.Length; i += 2)
            {
                if (i + 1 < results.Length && results[i + 1] is RedisResult[] fields)
                {
                    // Check score if available (for vector search results)
                    var score = 1.0f;
                    if (i > 1 && results[i - 1] is RedisResult scoreResult)
                    {
                        if (double.TryParse(scoreResult.ToString(), out var scoreValue))
                        {
                            // Redis returns distance, convert to similarity (1 - distance for cosine)
                            score = (float)(1.0 - scoreValue);
                        }
                    }

                    if (minScore.HasValue && score < minScore.Value)
                    {
                        continue;
                    }

                    var idStr = GetFieldValue(fields, "id");
                    var docIdStr = GetFieldValue(fields, "docId");
                    
                    if (Guid.TryParse(idStr, out var id) && Guid.TryParse(docIdStr, out var docId))
                    {
                        chunks.Add(new Chunk
                        {
                            Id = id,
                            DocumentId = docId,
                            DocumentName = GetFieldValue(fields, "docName"),
                            SourcePath = GetFieldValue(fields, "sourcePath"),
                            Hash = GetFieldValue(fields, "hash"),
                            Index = int.TryParse(GetFieldValue(fields, "chunkIndex"), out var idx) ? idx : 0,
                            Text = GetFieldValue(fields, "text"),
                            Snippet = GetFieldValue(fields, "snippet"),
                            CreatedUtc = long.TryParse(GetFieldValue(fields, "createdUtc"), out var ticks) 
                                ? new DateTime(ticks) 
                                : DateTime.UtcNow
                        });
                    }
                }
            }
        }

        return chunks;
    }

    private string GetFieldValue(RedisResult[] fields, string fieldName)
    {
        for (int i = 0; i < fields.Length - 1; i += 2)
        {
            if (fields[i].ToString() == fieldName)
            {
                return fields[i + 1].ToString() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
