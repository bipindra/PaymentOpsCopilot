using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Azure AI Search-backed implementation of <see cref="IVectorStore"/>.
/// </summary>
public class AzureAISearchVectorStore : IVectorStore
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;
    private readonly int _vectorSize;
    private readonly ILogger<AzureAISearchVectorStore> _logger;

    public AzureAISearchVectorStore(
        string serviceName,
        string indexName,
        string apiKey,
        int vectorSize,
        ILogger<AzureAISearchVectorStore> logger)
    {
        _indexName = indexName;
        _vectorSize = vectorSize;
        _logger = logger;

        var serviceEndpoint = new Uri($"https://{serviceName}.search.windows.net");
        var credential = new AzureKeyCredential(apiKey);
        
        _indexClient = new SearchIndexClient(serviceEndpoint, credential);
        _searchClient = new SearchClient(serviceEndpoint, indexName, credential);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var index = await _indexClient.GetIndexAsync(_indexName, cancellationToken);
            if (index != null)
            {
                _logger.LogInformation("Index {IndexName} already exists", _indexName);
                return;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Index doesn't exist, create it
        }

        var definition = new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                new SimpleField("docId", SearchFieldDataType.String) { IsFilterable = true, IsSortable = true },
                new SearchableField("docName") { IsFilterable = true, IsSortable = true },
                new SimpleField("sourcePath", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("hash", SearchFieldDataType.String) { IsFilterable = true },
                new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
                new SearchableField("text"),
                new SearchableField("snippet"),
                new SimpleField("createdUtc", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                new VectorSearchField("embedding", _vectorSize, "default")
            }
        };

        await _indexClient.CreateIndexAsync(definition, cancellationToken);
        _logger.LogInformation("Created index {IndexName}", _indexName);
    }

    public async Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        var documents = chunks.Select(chunk => new SearchDocument
        {
            ["id"] = chunk.Id.ToString(),
            ["docId"] = chunk.DocumentId.ToString(),
            ["docName"] = chunk.DocumentName,
            ["sourcePath"] = "",
            ["hash"] = chunk.Hash,
            ["chunkIndex"] = chunk.Index,
            ["text"] = chunk.Text,
            ["snippet"] = chunk.Snippet,
            ["createdUtc"] = chunk.CreatedUtc,
            ["embedding"] = chunk.Embedding ?? throw new InvalidOperationException("Chunk must have embedding")
        }).ToList();

        _logger.LogDebug("Upserting {Count} chunks to Azure AI Search", documents.Count);

        var batch = IndexDocumentsBatch.Upload(documents);
        var result = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

        if (result.Value.Results.Any(r => !r.Succeeded))
        {
            var errors = result.Value.Results.Where(r => !r.Succeeded).Select(r => r.ErrorMessage);
            _logger.LogError("Some documents failed to index: {Errors}", string.Join("; ", errors));
            throw new InvalidOperationException($"Failed to index some documents: {string.Join("; ", errors)}");
        }

        _logger.LogInformation("Upserted {Count} chunks", documents.Count);
    }

    public async Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default)
    {
        var searchOptions = new SearchOptions
        {
            Size = topK,
            VectorSearch = new VectorSearchOptions
            {
                Queries = { new VectorizedQuery(queryVector) { KNearestNeighborsCount = topK, Fields = { "embedding" } } }
            }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);

        var chunks = new List<Chunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            // Azure AI Search returns scores between 0 and 1, where 1 is most similar
            // We'll use the score directly, but note that minScore might need adjustment
            var score = result.Score ?? 0;
            if (minScore.HasValue && score < minScore.Value)
            {
                continue;
            }

            var doc = result.Document;
            chunks.Add(new Chunk
            {
                Id = Guid.Parse(doc["id"].ToString() ?? throw new InvalidOperationException("Missing id")),
                DocumentId = Guid.Parse(doc["docId"].ToString() ?? throw new InvalidOperationException("Missing docId")),
                DocumentName = doc["docName"]?.ToString() ?? string.Empty,
                Index = Convert.ToInt32(doc["chunkIndex"]),
                Text = doc["text"]?.ToString() ?? string.Empty,
                Snippet = doc["snippet"]?.ToString() ?? string.Empty,
                Hash = doc["hash"]?.ToString() ?? string.Empty,
                CreatedUtc = doc["createdUtc"] != null ? DateTimeOffset.Parse(doc["createdUtc"].ToString()!).DateTime : DateTime.UtcNow
            });
        }

        return chunks;
    }

    public async Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var searchOptions = new SearchOptions
        {
            Size = 10000,
            Select = { "docId", "docName", "sourcePath", "createdUtc" },
            IncludeTotalCount = true
        };

        var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);

        var docGroups = new Dictionary<Guid, Document>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            var docId = Guid.Parse(doc["docId"].ToString() ?? throw new InvalidOperationException("Missing docId"));
            
            if (!docGroups.ContainsKey(docId))
            {
                docGroups[docId] = new Document
                {
                    Id = docId,
                    Name = doc["docName"]?.ToString() ?? string.Empty,
                    SourcePath = doc["sourcePath"]?.ToString() ?? string.Empty,
                    CreatedUtc = doc["createdUtc"] != null ? DateTimeOffset.Parse(doc["createdUtc"].ToString()!).DateTime : DateTime.UtcNow,
                    ChunkCount = 0,
                    TotalSizeBytes = 0
                };
            }

            docGroups[docId].ChunkCount++;
            docGroups[docId].TotalSizeBytes += doc["text"]?.ToString()?.Length ?? 0;
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
        var filter = $"docId eq '{documentId}'";
        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = 10000,
            OrderBy = { "chunkIndex asc" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);

        var chunks = new List<Chunk>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            chunks.Add(new Chunk
            {
                Id = Guid.Parse(doc["id"].ToString() ?? throw new InvalidOperationException("Missing id")),
                DocumentId = Guid.Parse(doc["docId"].ToString() ?? throw new InvalidOperationException("Missing docId")),
                DocumentName = doc["docName"]?.ToString() ?? string.Empty,
                Index = Convert.ToInt32(doc["chunkIndex"]),
                Text = doc["text"]?.ToString() ?? string.Empty,
                Snippet = doc["snippet"]?.ToString() ?? string.Empty,
                Hash = doc["hash"]?.ToString() ?? string.Empty,
                CreatedUtc = doc["createdUtc"] != null ? DateTimeOffset.Parse(doc["createdUtc"].ToString()!).DateTime : DateTime.UtcNow
            });
        }

        return chunks.OrderBy(c => c.Index).ToList();
    }
}
