using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Mistral AI Embeddings API implementation.
/// </summary>
public class MistralAIEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<MistralAIEmbeddingClient> _logger;

    public MistralAIEmbeddingClient(string apiKey, string model, ILogger<MistralAIEmbeddingClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.mistral.ai/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GetEmbeddingsAsync(new[] { text }, cancellationToken);
        return embeddings[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        var request = new
        {
            model = _model,
            input = textList
        };

        _logger.LogDebug("Requesting embeddings for {Count} texts", textList.Count);

        var response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Mistral AI embedding API error: {Error}", error);
            throw new HttpRequestException($"Mistral AI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MistralAIEmbeddingResponse>(cancellationToken: cancellationToken);

        if (result?.Data == null || result.Data.Count != textList.Count)
        {
            throw new InvalidOperationException("Unexpected response from Mistral AI API");
        }

        return result.Data.Select(d => d.Embedding).ToArray();
    }

    private class MistralAIEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingData>? Data { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
