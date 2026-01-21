using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Calls the OpenAI Embeddings API to turn text into an embedding vector.
/// <para>
/// Embeddings power RAG retrieval: we embed chunks at ingest time, and embed the user question at query time,
/// then run similarity search in the vector database.
/// </para>
/// </summary>
public class OpenAIEmbeddingClient : IEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<OpenAIEmbeddingClient> _logger;

    public OpenAIEmbeddingClient(string apiKey, string model, ILogger<OpenAIEmbeddingClient> logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = TimeSpan.FromMinutes(5) // 5 minute timeout for embedding requests
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
        // The embeddings endpoint accepts a batch of inputs. We pass an array to reduce overhead.
        var request = new
        {
            model = _model,
            input = textList
        };

        _logger.LogDebug("Requesting embeddings for {Count} texts", textList.Count);
        
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync("embeddings", request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not a user cancellation)
            _logger.LogError("OpenAI embedding API request timed out after {Timeout}", _httpClient.Timeout);
            throw new HttpRequestException("OpenAI embedding API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenAI embedding API request was cancelled");
            throw new OperationCanceledException("Embedding request was cancelled", ex, cancellationToken);
        }
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI embedding API error: {Error}", error);
            throw new HttpRequestException($"OpenAI API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIEmbeddingResponse>(cancellationToken: cancellationToken);
        
        if (result?.Data == null || result.Data.Count != textList.Count)
        {
            throw new InvalidOperationException("Unexpected response from OpenAI API");
        }

        return result.Data.Select(d => d.Embedding).ToArray();
    }

    private class OpenAIEmbeddingResponse
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
