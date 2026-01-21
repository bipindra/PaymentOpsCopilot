using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Amazon Bedrock Embeddings API implementation.
/// </summary>
public class AmazonAIEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _region;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _model;
    private readonly ILogger<AmazonAIEmbeddingClient> _logger;

    public AmazonAIEmbeddingClient(string region, string accessKey, string secretKey, string model, ILogger<AmazonAIEmbeddingClient> logger)
    {
        _region = region;
        _accessKey = accessKey;
        _secretKey = secretKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"https://bedrock-runtime.{region}.amazonaws.com/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        // Note: In production, use AWS SDK for proper signing
        _httpClient.DefaultRequestHeaders.Add("x-amz-content-sha256", "UNSIGNED-PAYLOAD");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GetEmbeddingsAsync(new[] { text }, cancellationToken);
        return embeddings[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        var embeddings = new List<float[]>();

        // Amazon Bedrock processes one text at a time
        foreach (var text in textList)
        {
            var request = new
            {
                inputText = text
            };

            _logger.LogDebug("Requesting embedding for text (length: {Length})", text.Length);

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.Add("x-amz-target", $"com.amazonaws.bedrock.runtime.model.{_model}.InvokeModel");

            var response = await _httpClient.PostAsync($"model/{_model}/invoke", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Amazon Bedrock embedding API error: {Error}", error);
                throw new HttpRequestException($"Amazon Bedrock API error: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<AmazonAIEmbeddingResponse>(cancellationToken: cancellationToken);

            if (result?.Embedding == null)
            {
                throw new InvalidOperationException("Unexpected response from Amazon Bedrock API");
            }

            embeddings.Add(result.Embedding);
        }

        return embeddings.ToArray();
    }

    private class AmazonAIEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
