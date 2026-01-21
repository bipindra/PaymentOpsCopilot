namespace PaymentOps.Backend.Domain;

public class Document
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int ChunkCount { get; set; }
    public long TotalSizeBytes { get; set; }
}
