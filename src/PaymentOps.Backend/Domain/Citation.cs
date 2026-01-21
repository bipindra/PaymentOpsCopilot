namespace PaymentOps.Backend.Domain;

public class Citation
{
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public float? Score { get; set; }
}
