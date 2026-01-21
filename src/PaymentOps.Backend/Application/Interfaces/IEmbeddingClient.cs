namespace PaymentOps.Backend.Application.Interfaces;

/// <summary>
/// Turns text into an <b>embedding</b>: a numeric vector (array of floats) that captures semantic meaning.
/// Similar texts have embeddings that are close under a similarity metric (e.g., cosine similarity).
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>
    /// Gets a single embedding vector for one input string.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets embeddings for a batch of input strings. Batching improves throughput and reduces API overhead.
    /// </summary>
    Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
