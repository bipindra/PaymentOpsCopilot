namespace PaymentOps.Backend.Domain;

public class Chunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public DateTime CreatedUtc { get; set; }
}
