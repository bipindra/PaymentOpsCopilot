using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PaymentOps.Backend.Application.Services;

/// <summary>
/// Splits documents into smaller <b>chunks</b> for embedding + retrieval.
/// <para>
/// Why chunking? Embedding and searching a few relevant snippets is cheaper and more accurate than sending whole
/// runbooks to the LLM. Chunks also make citations deterministic via (docName, chunkIndex).
/// </para>
/// </summary>
public class ChunkingService
{
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly int _maxChunksPerDocument;

    public ChunkingService(IConfiguration configuration)
    {
        _chunkSize = configuration.GetValue<int>("RAG:ChunkSize", 1000);
        _chunkOverlap = configuration.GetValue<int>("RAG:ChunkOverlap", 150);
        _maxChunksPerDocument = configuration.GetValue<int>("RAG:MaxChunksPerDocument", 5000);

        if (_chunkSize <= 0)
            throw new InvalidOperationException("RAG:ChunkSize must be > 0");

        if (_chunkOverlap < 0)
            throw new InvalidOperationException("RAG:ChunkOverlap must be >= 0");

        // If overlap >= chunk size, the window can fail to advance (or re-process the tail forever).
        if (_chunkOverlap >= _chunkSize)
            throw new InvalidOperationException("RAG:ChunkOverlap must be < RAG:ChunkSize");

        if (_maxChunksPerDocument <= 0)
            throw new InvalidOperationException("RAG:MaxChunksPerDocument must be > 0");
    }

    /// <summary>
    /// Produces overlapping chunks from a single document.
    /// <para>
    /// <b>ChunkSize</b> and <b>ChunkOverlap</b> are measured in characters (not tokens).
    /// Overlap helps preserve meaning when a sentence spans a chunk boundary.
    /// </para>
    /// </summary>
    public List<ChunkInfo> ChunkText(string text, string documentName)
    {
        // Normalize whitespace but preserve structure:
        // - keep newlines so we can break on them
        // - collapse runs of spaces/tabs (not newlines) to a single space
        text = (text ?? string.Empty).Replace("\r\n", "\n");
        text = Regex.Replace(text, @"[ \t\f\v]+", " ");
        text = text.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return new List<ChunkInfo>();

        var chunks = new List<ChunkInfo>();
        var startIndex = 0;

        while (startIndex < text.Length)
        {
            if (chunks.Count >= _maxChunksPerDocument)
            {
                throw new InvalidOperationException(
                    $"Chunking produced too many chunks ({chunks.Count}). " +
                    $"Check RAG:ChunkSize/RAG:ChunkOverlap or increase RAG:MaxChunksPerDocument.");
            }

            // Calculate endIndex as EXCLUSIVE (can equal text.Length)
            var endIndex = Math.Min(startIndex + _chunkSize, text.Length);
            
            // Try to break at sentence boundary if not at end
            if (endIndex < text.Length)
            {
                // Search backwards from endIndex for sentence boundaries within the last 100 chars
                var searchStart = Math.Max(startIndex, endIndex - 100);
                var searchLength = endIndex - searchStart;
                
                var lastPeriod = text.LastIndexOf('.', endIndex - 1, searchLength);
                var lastNewline = text.LastIndexOf('\n', endIndex - 1, searchLength);
                var breakPoint = Math.Max(lastPeriod, lastNewline);
                
                // Only break if we found a boundary and it's at least halfway through the CURRENT window
                var halfWindow = Math.Max(1, (endIndex - startIndex) / 2);
                if (breakPoint >= startIndex + halfWindow)
                {
                    endIndex = breakPoint + 1;
                }
            }

            // Extract chunk text (endIndex is exclusive, so length is endIndex - startIndex)
            var chunkText = text.Substring(startIndex, endIndex - startIndex).Trim();
            
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                var snippet = chunkText.Length > 240 
                    ? chunkText.Substring(0, 240) + "..." 
                    : chunkText;
                
                var hash = ComputeHash(chunkText);
                
                chunks.Add(new ChunkInfo
                {
                    Text = chunkText,
                    Snippet = snippet,
                    Hash = hash,
                    Index = chunks.Count
                });
            }

            // If we reached the end, we're done. Do NOT apply overlap past the end,
            // otherwise we'll re-process the tail indefinitely.
            if (endIndex >= text.Length)
                break;

            // Move start index with overlap (must still advance)
            var nextStartIndex = Math.Max(0, endIndex - _chunkOverlap);
            if (nextStartIndex <= startIndex)
            {
                // With overlap close to chunk size, nextStartIndex might not advance; jump to endIndex.
                nextStartIndex = endIndex;
            }

            startIndex = nextStartIndex;
        }

        return chunks;
    }

    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class ChunkInfo
{
    public string Text { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Index { get; set; }
}
