using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Mistral AI Chat API implementation.
/// </summary>
public class MistralAIChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<MistralAIChatClient> _logger;

    public MistralAIChatClient(string apiKey, string model, ILogger<MistralAIChatClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.mistral.ai/v1/"),
            Timeout = TimeSpan.FromMinutes(2)
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
            temperature = 0.1
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Mistral AI chat API error: {Error}", error);
            throw new HttpRequestException($"Mistral AI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MistralAIChatResponse>(cancellationToken: cancellationToken);

        if (result?.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from Mistral AI API");
        }

        return new ChatResponse
        {
            Content = result.Choices[0].Message?.Content ?? string.Empty,
            TokensUsed = result.Usage?.TotalTokens
        };
    }

    private class MistralAIChatResponse
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
