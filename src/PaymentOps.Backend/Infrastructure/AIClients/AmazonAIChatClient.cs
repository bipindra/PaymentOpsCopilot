using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Amazon Bedrock Chat API implementation.
/// </summary>
public class AmazonAIChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _model;
    private readonly ILogger<AmazonAIChatClient> _logger;

    public AmazonAIChatClient(string region, string accessKey, string secretKey, string model, ILogger<AmazonAIChatClient> logger)
    {
        _region = region;
        _accessKey = accessKey;
        _secretKey = secretKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://bedrock-runtime.{region}.amazonaws.com/"),
            Timeout = TimeSpan.FromMinutes(2)
        };
        // Note: In production, use AWS SDK for proper signing
        _httpClient.DefaultRequestHeaders.Add("x-amz-content-sha256", "UNSIGNED-PAYLOAD");
    }

    public async Task<ChatResponse> GetCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.Add("x-amz-target", $"com.amazonaws.bedrock.runtime.model.{_model}.InvokeModel");

        var response = await _httpClient.PostAsync($"model/{_model}/invoke", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Amazon Bedrock chat API error: {Error}", error);
            throw new HttpRequestException($"Amazon Bedrock API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<AmazonAIChatResponse>(cancellationToken: cancellationToken);

        if (result?.Content == null || result.Content.Count == 0)
        {
            throw new InvalidOperationException("Unexpected response from Amazon Bedrock API");
        }

        return new ChatResponse
        {
            Content = result.Content[0].Text ?? string.Empty,
            TokensUsed = result.Usage?.InputTokens + result.Usage?.OutputTokens
        };
    }

    private class AmazonAIChatResponse
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
