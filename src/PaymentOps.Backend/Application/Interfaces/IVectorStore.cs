using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Application.Interfaces;

/// <summary>
/// A <b>vector store</b> persists embeddings and supports similarity search (RAG retrieval).
/// In this repo the implementation is Qdrant, but this interface keeps the app logic storage-agnostic.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Ensures the backing store is ready (e.g., creates the collection/index if missing).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts (inserts or updates) embedded chunks into the vector store so they can be retrieved later.
    /// </summary>
    Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Similarity-search the store for chunks close to <paramref name="queryVector"/>.
    /// </summary>
    Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default);
    Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<Document?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<List<Chunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default);
}
