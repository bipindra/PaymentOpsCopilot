namespace PaymentOps.Backend.DTOs;

public class AskRequest
{
    public string Question { get; set; } = string.Empty;
    public int TopK { get; set; } = 5;
}
