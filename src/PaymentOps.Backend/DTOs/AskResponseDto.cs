using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.DTOs;

public class AskResponseDto
{
    public string AnswerMarkdown { get; set; } = string.Empty;
    public List<CitationDto> Citations { get; set; } = new();
    public List<CitationDto> Retrieved { get; set; } = new();
    public long ElapsedMs { get; set; }
    public int? TokensUsed { get; set; }
}

public class CitationDto
{
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public float? Score { get; set; }
}
