using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Microsoft Azure OpenAI Embeddings API implementation.
/// </summary>
public class MicrosoftAIEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<MicrosoftAIEmbeddingClient> _logger;

    public MicrosoftAIEmbeddingClient(string endpoint, string apiKey, string model, ILogger<MicrosoftAIEmbeddingClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        var baseUrl = endpoint.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{baseUrl}/openai/deployments/{model}/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
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
            input = textList
        };

        _logger.LogDebug("Requesting embeddings for {Count} texts", textList.Count);

        var response = await _httpClient.PostAsJsonAsync("embeddings?api-version=2023-05-15", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Azure OpenAI embedding API error: {Error}", error);
            throw new HttpRequestException($"Azure OpenAI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<MicrosoftAIEmbeddingResponse>(cancellationToken: cancellationToken);

        if (result?.Data == null || result.Data.Count != textList.Count)
        {
            throw new InvalidOperationException("Unexpected response from Azure OpenAI API");
        }

        return result.Data.Select(d => d.Embedding).ToArray();
    }

    private class MicrosoftAIEmbeddingResponse
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
