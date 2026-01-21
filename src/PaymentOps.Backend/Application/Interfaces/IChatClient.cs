namespace PaymentOps.Backend.Application.Interfaces;

/// <summary>
/// Sends prompts to a chat-capable LLM (Large Language Model) and returns the generated completion.
/// </summary>
public interface IChatClient
{
    /// <summary>
    /// Requests a completion from the LLM using a system prompt (rules) and user prompt (question + context).
    /// </summary>
    Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM response payload returned by <see cref="IChatClient"/>.
/// </summary>
public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public int? TokensUsed { get; set; }
}
