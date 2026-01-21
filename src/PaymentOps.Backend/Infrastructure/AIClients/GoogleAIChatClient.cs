using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Google AI (Gemini) Chat API implementation.
/// </summary>
public class GoogleAIChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GoogleAIChatClient> _logger;

    public GoogleAIChatClient(string apiKey, string model, ILogger<GoogleAIChatClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public async Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = $"{systemPrompt}\n\n{userPrompt}" }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_model}:generateContent?key={_apiKey}",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google AI chat API error: {Error}", error);
            throw new HttpRequestException($"Google AI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<GoogleAIChatResponse>(cancellationToken: cancellationToken);

        if (result?.Candidates == null || result.Candidates.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from Google AI API");
        }

        return new ChatResponse
        {
            Content = result.Candidates[0].Content?.Parts?[0]?.Text ?? string.Empty,
            TokensUsed = result.UsageMetadata?.TotalTokenCount
        };
    }

    private class GoogleAIChatResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class UsageMetadata
    {
        [JsonPropertyName("totalTokenCount")]
        public int? TotalTokenCount { get; set; }
    }
}
