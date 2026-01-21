using System.Diagnostics;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Application.Services;

/// <summary>
/// Ingests documents into the RAG knowledge base:
/// <list type="number">
/// <item><description>Chunk the document into small snippets.</description></item>
/// <item><description>Create an <b>embedding</b> vector for each chunk.</description></item>
/// <item><description>Upsert the (embedding + metadata + text) into the <b>vector store</b> (Qdrant).</description></item>
/// </list>
/// </summary>
public class IngestService
{
    private static readonly ActivitySource ActivitySource = new("PaymentOps.Ingest");
    private readonly ChunkingService _chunkingService;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IngestService> _logger;
    private readonly int _embeddingBatchSize;
    private readonly int _vectorStoreBatchSize;
    private readonly long _maxFileSizeBytes;

    public IngestService(
        ChunkingService chunkingService,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        IConfiguration configuration,
        ILogger<IngestService> logger)
    {
        _chunkingService = chunkingService;
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _configuration = configuration;
        _logger = logger;
        _embeddingBatchSize = configuration.GetValue<int>("RAG:EmbeddingBatchSize", 100);
        _vectorStoreBatchSize = configuration.GetValue<int>("RAG:VectorStoreBatchSize", 50);
        _maxFileSizeBytes = configuration.GetValue<long>("RAG:MaxFileSizeBytes", 10485760); // 10MB default
    }

    /// <summary>
    /// Ingests a single text document and returns document metadata.
    /// </summary>
    public async Task<Document> IngestTextAsync(string docName, string text, string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("IngestText");
        activity?.SetTag("docName", docName);

        var documentId = Guid.NewGuid();
        var createdUtc = DateTime.UtcNow;

        // Chunk the text: we embed/search chunks (snippets), not whole documents.
        var chunkInfos = _chunkingService.ChunkText(text, docName);
        _logger.LogInformation("Chunked document {DocName} into {Count} chunks", docName, chunkInfos.Count);

        if (chunkInfos.Count == 0)
        {
            throw new InvalidOperationException("No chunks created from text");
        }

        // Process chunks in batches to avoid memory issues and reduce API overhead.
        var totalChunksStored = 0;
        var chunkInfoList = chunkInfos.ToList();
        
        // Embedding models accept multiple inputs in one call; batching improves throughput and cost.
        for (int i = 0; i < chunkInfoList.Count; i += _embeddingBatchSize)
        {
            var batch = chunkInfoList.Skip(i).Take(_embeddingBatchSize).ToList();
            var batchTexts = batch.Select(c => c.Text).ToList();
            
            var batchNum = (i / _embeddingBatchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)chunkInfoList.Count / _embeddingBatchSize);
            _logger.LogInformation("Processing embedding batch {BatchNum} of {TotalBatches} ({Count} chunks)", 
                batchNum, totalBatches, batch.Count);
            
            var batchEmbeddings = await _embeddingClient.GetEmbeddingsAsync(batchTexts, cancellationToken);
            _logger.LogDebug("Completed embedding batch {BatchNum} of {TotalBatches}", batchNum, totalBatches);
            
            // Create chunk entities for this batch
            var batchChunks = batch.Select((chunkInfo, batchIndex) => new Chunk
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                DocumentName = docName,
                Index = chunkInfo.Index,
                Text = chunkInfo.Text,
                Snippet = chunkInfo.Snippet,
                Hash = chunkInfo.Hash,
                Embedding = batchEmbeddings[batchIndex],
                CreatedUtc = createdUtc
            }).ToList();
            
            // Store chunks in the vector DB in batches (keeps requests and payload sizes reasonable).
            for (int j = 0; j < batchChunks.Count; j += _vectorStoreBatchSize)
            {
                var storeBatch = batchChunks.Skip(j).Take(_vectorStoreBatchSize).ToList();
                _logger.LogDebug("Storing vector store batch {StoreBatchNum} ({Count} chunks)", 
                    (j / _vectorStoreBatchSize) + 1, storeBatch.Count);
                await _vectorStore.UpsertChunksAsync(storeBatch, cancellationToken);
            }

            totalChunksStored += batchChunks.Count;
        }
        
        _logger.LogInformation("Generated embeddings and stored {Count} chunks in vector store", totalChunksStored);

        // Create document
        var document = new Document
        {
            Id = documentId,
            Name = docName,
            SourcePath = sourcePath ?? "",
            CreatedUtc = createdUtc,
            ChunkCount = totalChunksStored,
            TotalSizeBytes = text.Length
        };

        return document;
    }

    public async Task<List<Document>> IngestFilesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();

        foreach (var filePath in filePaths)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File not found: {FilePath}", filePath);
                    continue;
                }

                var fileName = Path.GetFileName(filePath);
                
                // Check file size before reading
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > _maxFileSizeBytes)
                {
                    _logger.LogWarning("File {FilePath} is too large ({Size} bytes, max {MaxSize} bytes). Skipping.", 
                        filePath, fileInfo.Length, _maxFileSizeBytes);
                    continue;
                }
                
                var text = await File.ReadAllTextAsync(filePath, cancellationToken);
                
                var document = await IngestTextAsync(fileName, text, filePath, cancellationToken);
                documents.Add(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest file: {FilePath}", filePath);
            }
        }

        return documents;
    }
}
