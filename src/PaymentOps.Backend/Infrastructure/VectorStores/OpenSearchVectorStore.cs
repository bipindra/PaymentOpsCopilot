using OpenSearch.Client;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Amazon OpenSearch implementation of <see cref="IVectorStore"/>.
/// </summary>
public class OpenSearchVectorStore : IVectorStore
{
    private readonly IOpenSearchClient _client;
    private readonly string _indexName;
    private readonly int _vectorSize;
    private readonly ILogger<OpenSearchVectorStore> _logger;

    public OpenSearchVectorStore(
        string uri,
        string indexName,
        string? username,
        string? password,
        int vectorSize,
        ILogger<OpenSearchVectorStore> logger)
    {
        _indexName = indexName;
        _vectorSize = vectorSize;
        _logger = logger;

        var connectionSettings = new ConnectionSettings(new Uri(uri));
        
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            connectionSettings.BasicAuthentication(username, password);
        }

        _client = new OpenSearchClient(connectionSettings);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var existsResponse = await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken);
        if (existsResponse.Exists)
        {
            _logger.LogInformation("Index {IndexName} already exists", _indexName);
            return;
        }

        // Create index with k-NN vector field using raw mapping
        // Note: This requires OpenSearch with k-NN plugin enabled
        var mappingJson = $@"{{
            ""settings"": {{
                ""number_of_shards"": 1,
                ""number_of_replicas"": 0,
                ""index"": {{
                    ""knn"": true,
                    ""knn.algo_param.ef_search"": 100
                }}
            }},
            ""mappings"": {{
                ""properties"": {{
                    ""id"": {{ ""type"": ""keyword"" }},
                    ""docId"": {{ ""type"": ""keyword"" }},
                    ""docName"": {{ ""type"": ""text"" }},
                    ""sourcePath"": {{ ""type"": ""text"" }},
                    ""hash"": {{ ""type"": ""keyword"" }},
                    ""chunkIndex"": {{ ""type"": ""integer"" }},
                    ""text"": {{ ""type"": ""text"" }},
                    ""snippet"": {{ ""type"": ""text"" }},
                    ""embedding"": {{
                        ""type"": ""knn_vector"",
                        ""dimension"": {_vectorSize},
                        ""method"": {{
                            ""name"": ""hnsw"",
                            ""space_type"": ""cosinesimil"",
                            ""engine"": ""nmslib""
                        }}
                    }},
                    ""createdUtc"": {{ ""type"": ""date"" }}
                }}
            }}
        }}";

        // Use low-level API to create index with custom mapping
        var createIndexResponse = await _client.LowLevel.Indices.CreateAsync<OpenSearch.Net.StringResponse>(_indexName, mappingJson, new OpenSearch.Net.Specification.IndicesApi.CreateIndexRequestParameters(), cancellationToken);

        if (!createIndexResponse.Success)
        {
            throw new InvalidOperationException($"Failed to create index: {createIndexResponse.DebugInformation}");
        }

        _logger.LogInformation("Created index {IndexName}", _indexName);
    }

    public async Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (chunksList.Count == 0) return;

        _logger.LogDebug("Upserting {Count} chunks to OpenSearch", chunksList.Count);

        var bulkDescriptor = new BulkDescriptor();
        foreach (var chunk in chunksList)
        {
            if (chunk.Embedding == null)
            {
                throw new InvalidOperationException("Chunk must have embedding");
            }

            var doc = new
            {
                id = chunk.Id.ToString(),
                docId = chunk.DocumentId.ToString(),
                docName = chunk.DocumentName,
                sourcePath = string.Empty,
                hash = chunk.Hash,
                chunkIndex = chunk.Index,
                text = chunk.Text,
                snippet = chunk.Snippet,
                embedding = chunk.Embedding,
                createdUtc = chunk.CreatedUtc
            };

            bulkDescriptor.Index<object>(op => op
                .Index(_indexName)
                .Id(chunk.Id.ToString())
                .Document(doc));
        }

        var response = await _client.BulkAsync(bulkDescriptor, cancellationToken);
        
        if (!response.IsValid)
        {
            _logger.LogError("Failed to bulk index chunks: {Error}", response.DebugInformation);
            throw new InvalidOperationException($"Failed to index chunks: {response.DebugInformation}");
        }

        if (response.Errors)
        {
            var errors = response.ItemsWithErrors.Select(i => i.Error?.Reason);
            _logger.LogError("Some documents failed to index: {Errors}", string.Join("; ", errors));
        }

        _logger.LogInformation("Upserted {Count} chunks", chunksList.Count);
    }

    public async Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default)
    {
        var searchResponse = await _client.SearchAsync<OpenSearchDocument>(s => s
            .Index(_indexName)
            .Size(topK)
            .Query(q => q
                .Raw(@"{""knn"": {""field"": ""embedding"", ""query_vector"": " + System.Text.Json.JsonSerializer.Serialize(queryVector) + @", ""k"": " + topK + @"}}"))
            .Source(src => src
                .Includes(i => i
                    .Fields("id", "docId", "docName", "sourcePath", "hash", "chunkIndex", "text", "snippet", "createdUtc"))),
            cancellationToken);

        if (!searchResponse.IsValid)
        {
            throw new InvalidOperationException($"Search failed: {searchResponse.DebugInformation}");
        }

        var chunks = new List<Chunk>();
        foreach (var hit in searchResponse.Hits)
        {
            // OpenSearch k-NN returns scores where higher is better
            // The score is typically a similarity score
            var score = hit.Score ?? 0;
            if (minScore.HasValue && score < minScore.Value)
            {
                continue;
            }

            var doc = hit.Source;
            chunks.Add(new Chunk
            {
                Id = Guid.Parse(doc.Id),
                DocumentId = Guid.Parse(doc.DocId),
                DocumentName = doc.DocName,
                Hash = doc.Hash,
                Index = doc.ChunkIndex,
                Text = doc.Text,
                Snippet = doc.Snippet,
                CreatedUtc = doc.CreatedUtc
            });
        }

        return chunks;
    }

    public async Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var searchResponse = await _client.SearchAsync<OpenSearchDocument>(s => s
            .Index(_indexName)
            .Size(10000)
            .Source(src => src
                .Includes(i => i
                    .Fields("docId", "docName", "sourcePath", "createdUtc", "text"))),
            cancellationToken);

        if (!searchResponse.IsValid)
        {
            throw new InvalidOperationException($"Failed to get documents: {searchResponse.DebugInformation}");
        }

        var docGroups = new Dictionary<Guid, Document>();
        foreach (var hit in searchResponse.Hits)
        {
            var doc = hit.Source;
            var docId = Guid.Parse(doc.DocId);
            
            if (!docGroups.ContainsKey(docId))
            {
                docGroups[docId] = new Document
                {
                    Id = docId,
                    Name = doc.DocName,
                    CreatedUtc = doc.CreatedUtc,
                    ChunkCount = 0,
                    TotalSizeBytes = 0
                };
            }

            docGroups[docId].ChunkCount++;
            docGroups[docId].TotalSizeBytes += doc.Text?.Length ?? 0;
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
        var searchResponse = await _client.SearchAsync<OpenSearchDocument>(s => s
            .Index(_indexName)
            .Size(10000)
            .Query(q => q
                .Term(t => t
                    .Field("docId")
                    .Value(documentId.ToString())))
            .Sort(sort => sort
                .Ascending("chunkIndex"))
            .Source(src => src
                .Includes(i => i
                    .Fields("id", "docId", "docName", "sourcePath", "hash", "chunkIndex", "text", "snippet", "createdUtc"))),
            cancellationToken);

        if (!searchResponse.IsValid)
        {
            throw new InvalidOperationException($"Failed to get document chunks: {searchResponse.DebugInformation}");
        }

        return searchResponse.Hits.Select(hit =>
        {
            var doc = hit.Source;
            return new Chunk
            {
                Id = Guid.Parse(doc.Id),
                DocumentId = Guid.Parse(doc.DocId),
                DocumentName = doc.DocName,
                Hash = doc.Hash,
                Index = doc.ChunkIndex,
                Text = doc.Text,
                Snippet = doc.Snippet,
                CreatedUtc = doc.CreatedUtc
            };
        }).OrderBy(c => c.Index).ToList();
    }

    private class OpenSearchDocument
    {
        public string Id { get; set; } = string.Empty;
        public string DocId { get; set; } = string.Empty;
        public string DocName { get; set; } = string.Empty;
        public string? SourcePath { get; set; }
        public string Hash { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }
}
