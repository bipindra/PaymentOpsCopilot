using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Anthropic Claude Chat API implementation.
/// Note: Anthropic does not provide an embedding API.
/// </summary>
public class AnthropicChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<AnthropicChatClient> _logger;

    public AnthropicChatClient(string apiKey, string model, ILogger<AnthropicChatClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.anthropic.com/v1/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = _model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1
        };

        var response = await _httpClient.PostAsJsonAsync("messages", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Anthropic chat API error: {Error}", error);
            throw new HttpRequestException($"Anthropic API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<AnthropicChatResponse>(cancellationToken: cancellationToken);

        if (result?.Content == null || result.Content.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from Anthropic API");
        }

        return new ChatResponse
        {
            Content = result.Content[0].Text ?? string.Empty,
            TokensUsed = result.Usage?.InputTokens + result.Usage?.OutputTokens
        };
    }

    private class AnthropicChatResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int? InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int? OutputTokens { get; set; }
    }
}
