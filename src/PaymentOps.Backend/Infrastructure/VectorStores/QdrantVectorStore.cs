using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Qdrant-backed implementation of <see cref="IVectorStore"/>.
/// <para>
/// A vector store lets us do <b>similarity search</b> over embeddings (RAG retrieval). We store each chunk's
/// embedding vector alongside payload metadata (doc id/name, chunk index, snippet, etc.).
/// </para>
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _collectionName;
    private readonly int _vectorSize;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(string baseUrl, string collectionName, int vectorSize, ILogger<QdrantVectorStore> logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _collectionName = collectionName;
        _vectorSize = vectorSize;
        _logger = logger;
        _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(2) // 2 minute timeout for Qdrant operations
        };
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Check if collection exists
        var checkResponse = await _httpClient.GetAsync($"/collections/{_collectionName}", cancellationToken);
        
        if (checkResponse.IsSuccessStatusCode)
        {
            _logger.LogInformation("Collection {CollectionName} already exists", _collectionName);
            return;
        }

        // Create collection with cosine distance (typical for embedding similarity).
        var createRequest = new
        {
            vectors = new
            {
                size = _vectorSize,
                distance = "Cosine"
            }
        };

        var createResponse = await _httpClient.PutAsJsonAsync(
            $"/collections/{_collectionName}",
            createRequest,
            cancellationToken);

        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create collection: {error}");
        }

        _logger.LogInformation("Created collection {CollectionName}", _collectionName);
    }

    public async Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        var points = chunks.Select(chunk => new
        {
            id = chunk.Id.ToString(),
            vector = chunk.Embedding ?? throw new InvalidOperationException("Chunk must have embedding"),
            payload = new
            {
                docId = chunk.DocumentId.ToString(),
                docName = chunk.DocumentName,
                sourcePath = "",
                hash = chunk.Hash,
                chunkIndex = chunk.Index,
                text = chunk.Text,
                snippet = chunk.Snippet,
                createdUtc = chunk.CreatedUtc.ToString("O")
            }
        }).ToList();

        var request = new
        {
            points = points
        };

        _logger.LogDebug("Upserting {Count} chunks to Qdrant", points.Count);
        
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                request,
                cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not a user cancellation)
            _logger.LogError("Qdrant upsert request timed out after {Timeout}", _httpClient.Timeout);
            throw new InvalidOperationException("Qdrant upsert request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Qdrant upsert request was cancelled");
            throw new OperationCanceledException("Upsert request was cancelled", ex, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Qdrant upsert failed: {Error}", error);
            throw new InvalidOperationException($"Failed to upsert chunks: {error}");
        }

        _logger.LogInformation("Upserted {Count} chunks", points.Count);
    }

    public async Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default)
    {
        // score_threshold is optional. When set, weak matches are filtered out (but you may get 0 results).
        var request = new
        {
            vector = queryVector,
            limit = topK,
            score_threshold = minScore,
            with_payload = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/search",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Search failed: {error}");
        }

        var searchResult = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(cancellationToken: cancellationToken);
        
        return searchResult?.Result?.Select(r => new Chunk
        {
            Id = Guid.Parse(r.Id),
            DocumentId = Guid.Parse(r.Payload.DocId),
            DocumentName = r.Payload.DocName,
            Index = r.Payload.ChunkIndex,
            Text = r.Payload.Text,
            Snippet = r.Payload.Snippet,
            Hash = r.Payload.Hash,
            CreatedUtc = DateTime.Parse(r.Payload.CreatedUtc)
        }).ToList() ?? new List<Chunk>();
    }

    public async Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        // Scroll through all points to aggregate by document
        var request = new
        {
            limit = 10000,
            with_payload = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/scroll",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to get documents: {error}");
        }

        var scrollResult = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(cancellationToken: cancellationToken);
        
        var docGroups = scrollResult?.Result?.Points
            .GroupBy(p => p.Payload.DocId)
            .Select(g =>
            {
                var first = g.First();
                return new Document
                {
                    Id = Guid.Parse(first.Payload.DocId),
                    Name = first.Payload.DocName,
                    SourcePath = first.Payload.SourcePath ?? "",
                    ChunkCount = g.Count(),
                    CreatedUtc = g.Min(p => DateTime.Parse(p.Payload.CreatedUtc)),
                    TotalSizeBytes = g.Sum(p => p.Payload.Text?.Length ?? 0)
                };
            })
            .ToList() ?? new List<Document>();

        return docGroups;
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var documents = await GetDocumentsAsync(cancellationToken);
        return documents.FirstOrDefault(d => d.Id == documentId);
    }

    public async Task<List<Chunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var filter = new
        {
            must = new[]
            {
                new
                {
                    key = "docId",
                    match = new { value = documentId.ToString() }
                }
            }
        };

        var request = new
        {
            filter = filter,
            limit = 10000,
            with_payload = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"/collections/{_collectionName}/points/scroll",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to get document chunks: {error}");
        }

        var scrollResult = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(cancellationToken: cancellationToken);
        
        return scrollResult?.Result?.Points
            .Select(p => new Chunk
            {
                Id = Guid.Parse(p.Id),
                DocumentId = Guid.Parse(p.Payload.DocId),
                DocumentName = p.Payload.DocName,
                Index = p.Payload.ChunkIndex,
                Text = p.Payload.Text,
                Snippet = p.Payload.Snippet,
                Hash = p.Payload.Hash,
                CreatedUtc = DateTime.Parse(p.Payload.CreatedUtc)
            })
            .OrderBy(c => c.Index)
            .ToList() ?? new List<Chunk>();
    }

    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResult>? Result { get; set; }
    }

    private class QdrantSearchResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("score")]
        public float Score { get; set; }
        
        [JsonPropertyName("payload")]
        public QdrantPayload Payload { get; set; } = new();
    }

    private class QdrantScrollResponse
    {
        [JsonPropertyName("result")]
        public QdrantScrollResult? Result { get; set; }
    }

    private class QdrantScrollResult
    {
        [JsonPropertyName("points")]
        public List<QdrantPoint>? Points { get; set; }
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("payload")]
        public QdrantPayload Payload { get; set; } = new();
    }

    private class QdrantPayload
    {
        [JsonPropertyName("docId")]
        public string DocId { get; set; } = string.Empty;
        
        [JsonPropertyName("docName")]
        public string DocName { get; set; } = string.Empty;
        
        [JsonPropertyName("sourcePath")]
        public string? SourcePath { get; set; }
        
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;
        
        [JsonPropertyName("chunkIndex")]
        public int ChunkIndex { get; set; }
        
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
        
        [JsonPropertyName("snippet")]
        public string Snippet { get; set; } = string.Empty;
        
        [JsonPropertyName("createdUtc")]
        public string CreatedUtc { get; set; } = string.Empty;
    }
}
