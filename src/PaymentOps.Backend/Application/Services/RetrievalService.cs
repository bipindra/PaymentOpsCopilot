using System.Diagnostics;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Application.Services;

/// <summary>
/// RAG retrieval step: turns a user question into an embedding and runs similarity search
/// to find the most relevant document chunks.
/// </summary>
public class RetrievalService
{
    private static readonly ActivitySource ActivitySource = new("PaymentOps.Retrieval");
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        IConfiguration configuration,
        ILogger<RetrievalService> logger)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the top-K most similar chunks for a query.
    /// <para>
    /// <paramref name="topK"/> controls recall (more context) vs. cost (longer prompts).
    /// <c>RAG:MinSimilarityScore</c> can filter weak matches but may return zero chunks if set too high.
    /// </para>
    /// </summary>
    public async Task<List<Chunk>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Retrieve");
        activity?.SetTag("query", query);
        activity?.SetTag("topK", topK);

        // Convert the question to an embedding vector so we can compare it to embedded chunks.
        var queryEmbedding = await _embeddingClient.GetEmbeddingAsync(query, cancellationToken);
        
        // Get minimum similarity score from config (null = no threshold)
        var minScore = _configuration.GetValue<float?>("RAG:MinSimilarityScore");
        if (minScore.HasValue)
        {
            _logger.LogDebug("Using similarity threshold: {MinScore}", minScore.Value);
        }
        else
        {
            _logger.LogDebug("No similarity threshold - returning top {TopK} results", topK);
        }

        // Search vector store
        var chunks = await _vectorStore.SearchAsync(queryEmbedding, topK, minScore, cancellationToken);
        
        _logger.LogInformation("Retrieved {Count} chunks for query '{Query}' (topK={TopK}, minScore={MinScore})", 
            chunks.Count, query, topK, minScore?.ToString() ?? "none");
        
        if (chunks.Count == 0 && minScore.HasValue)
        {
            _logger.LogWarning("No chunks retrieved with threshold {MinScore}. Try lowering MinSimilarityScore in appsettings.json", minScore.Value);
        }
        
        return chunks;
    }
}
