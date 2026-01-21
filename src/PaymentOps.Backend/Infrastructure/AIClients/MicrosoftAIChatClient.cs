using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Microsoft Azure OpenAI Chat API implementation.
/// </summary>
public class MicrosoftAIChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<MicrosoftAIChatClient> _logger;

    public MicrosoftAIChatClient(string endpoint, string apiKey, string model, ILogger<MicrosoftAIChatClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        var baseUrl = endpoint.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{baseUrl}/openai/deployments/{model}/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.1
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions?api-version=2023-05-15", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Azure OpenAI chat API error: {Error}", error);
            throw new HttpRequestException($"Azure OpenAI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MicrosoftAIChatResponse>(cancellationToken: cancellationToken);

        if (result?.Choices == null || result.Choices.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from Azure OpenAI API");
        }

        return new ChatResponse
        {
            Content = result.Choices[0].Message?.Content ?? string.Empty,
            TokensUsed = result.Usage?.TotalTokens
        };
    }

    private class MicrosoftAIChatResponse
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
