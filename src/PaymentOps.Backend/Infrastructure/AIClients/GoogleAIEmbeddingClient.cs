using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Google AI (Gemini) Embeddings API implementation.
/// </summary>
public class GoogleAIEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GoogleAIEmbeddingClient> _logger;

    public GoogleAIEmbeddingClient(string apiKey, string model, ILogger<GoogleAIEmbeddingClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
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

        // Google AI processes one text at a time
        foreach (var text in textList)
        {
            var request = new
            {
                model = $"models/{_model}",
                content = new { parts = new[] { new { text } } }
            };

            _logger.LogDebug("Requesting embedding for text (length: {Length})", text.Length);

            var response = await _httpClient.PostAsJsonAsync(
                $"{_model}:embedContent?key={_apiKey}",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Google AI embedding API error: {Error}", error);
                throw new HttpRequestException($"Google AI API error: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleAIEmbeddingResponse>(cancellationToken: cancellationToken);
            
            if (result?.Embedding?.Values == null)
            {
                throw new InvalidOperationException("Unexpected response from Google AI API");
            }

            embeddings.Add(result.Embedding.Values);
        }

        return embeddings.ToArray();
    }

    private class GoogleAIEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public EmbeddingData? Embedding { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
