namespace PaymentOps.Backend.DTOs;

public class IngestRequest
{
    public string DocName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
