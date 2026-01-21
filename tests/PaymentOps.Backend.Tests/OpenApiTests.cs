using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PaymentOps.Backend;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PaymentOps.Backend.Tests;

public class OpenApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public OpenApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "AI:Provider", "OpenAI" },
                    { "AI:OpenAI:ApiKey", "test-key" },
                    { "AI:OpenAI:EmbeddingModel", "text-embedding-3-small" },
                    { "AI:OpenAI:ChatModel", "gpt-4o-mini" },
                    { "VectorStore:Provider", "Qdrant" },
                    { "VectorStore:VectorSize", "1536" },
                    { "VectorStore:Qdrant:BaseUrl", "http://localhost:6333" },
                    { "VectorStore:Qdrant:CollectionName", "test" },
                    { "Cors:AllowedOrigins:0", "http://localhost:4200" }
                });
            });
            builder.ConfigureServices(services =>
            {
                // Prevent vector store initialization from failing tests
                // The initialization is async and happens in Program.cs, but we'll let it fail gracefully
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerJson_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("openapi", content);
        Assert.Contains("info", content);
    }

    [Fact]
    public async Task SwaggerUI_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/swagger");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("swagger", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerJson_ShouldContainApiEndpoints()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("paths", content);
        // Check for common endpoints
        Assert.Contains("/api/ask", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SwaggerJson_ShouldHaveValidOpenApiVersion()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Assert
        Assert.True(json.TryGetProperty("openapi", out var openApiVersion));
        var version = openApiVersion.GetString();
        Assert.NotNull(version);
        Assert.StartsWith("3.", version);
    }
}
