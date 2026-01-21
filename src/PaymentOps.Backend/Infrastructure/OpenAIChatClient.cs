using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Calls the OpenAI Chat Completions API to generate an answer from prompts.
/// <para>
/// In RAG, we provide the LLM with a system prompt (rules) and a user prompt (question + retrieved context).
/// Keeping temperature low reduces creativity and helps avoid hallucinations.
/// </para>
/// </summary>
public class OpenAIChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIChatClient> _logger;

    public OpenAIChatClient(string apiKey, string model, ILogger<OpenAIChatClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromMinutes(2) // 2 minute timeout for chat requests
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            // Lower temperature = more deterministic, less "creative" outputs (typically better for ops runbooks).
            temperature = 0.1
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI chat API error: {Error}", error);
            throw new HttpRequestException($"OpenAI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIChatResponse>(cancellationToken: cancellationToken);
        
        if (result?.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from OpenAI API");
        }

        return new ChatResponse
        {
            Content = result.Choices[0].Message.Content ?? string.Empty,
            TokensUsed = result.Usage?.TotalTokens
        };
    }

    private class OpenAIChatResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
        
        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("total_tokens")]
        public int? TotalTokens { get; set; }
    }
}
