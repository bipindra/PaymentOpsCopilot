namespace PaymentOps.Backend.Domain;

public class AskResponse
{
    public string AnswerMarkdown { get; set; } = string.Empty;
    public List<Citation> Citations { get; set; } = new();
    public List<Citation> Retrieved { get; set; } = new();
    public long ElapsedMs { get; set; }
    public int? TokensUsed { get; set; }
}
