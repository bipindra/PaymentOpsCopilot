using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// Factory for creating the appropriate AI client implementations based on configuration.
/// </summary>
public static class AIClientFactory
{
    /// <summary>
    /// Creates an IEmbeddingClient instance based on the configured provider.
    /// </summary>
    public static IEmbeddingClient CreateEmbeddingClient(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var provider = configuration["AI:Provider"] ?? "OpenAI";
        var logger = loggerFactory.CreateLogger($"AIClientFactory");

        logger.LogInformation("Creating embedding client with provider: {Provider}", provider);

        return provider.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIEmbedding(configuration, loggerFactory),
            "google" or "googleai" => CreateGoogleAIEmbedding(configuration, loggerFactory),
            "microsoft" or "azureopenai" => CreateMicrosoftAIEmbedding(configuration, loggerFactory),
            "amazon" or "bedrock" => CreateAmazonAIEmbedding(configuration, loggerFactory),
            "anthropic" => CreateAnthropicEmbedding(configuration, loggerFactory),
            "mistral" or "mistralai" => CreateMistralAIEmbedding(configuration, loggerFactory),
            _ => throw new InvalidOperationException($"Unknown AI provider: {provider}. Supported providers: OpenAI, Google, Microsoft, Amazon, Anthropic, Mistral")
        };
    }

    /// <summary>
    /// Creates an IChatClient instance based on the configured provider.
    /// </summary>
    public static IChatClient CreateChatClient(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var provider = configuration["AI:Provider"] ?? "OpenAI";
        var logger = loggerFactory.CreateLogger($"AIClientFactory");

        logger.LogInformation("Creating chat client with provider: {Provider}", provider);

        return provider.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIChat(configuration, loggerFactory),
            "google" or "googleai" => CreateGoogleAIChat(configuration, loggerFactory),
            "microsoft" or "azureopenai" => CreateMicrosoftAIChat(configuration, loggerFactory),
            "amazon" or "bedrock" => CreateAmazonAIChat(configuration, loggerFactory),
            "anthropic" => CreateAnthropicChat(configuration, loggerFactory),
            "mistral" or "mistralai" => CreateMistralAIChat(configuration, loggerFactory),
            _ => throw new InvalidOperationException($"Unknown AI provider: {provider}. Supported providers: OpenAI, Google, Microsoft, Amazon, Anthropic, Mistral")
        };
    }

    private static IEmbeddingClient CreateOpenAIEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:OpenAI:ApiKey"] 
            ?? configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AI:OpenAI:ApiKey is required");
        var model = configuration["AI:OpenAI:EmbeddingModel"] 
            ?? configuration["OpenAI:EmbeddingModel"] 
            ?? "text-embedding-3-small";
        
        var logger = loggerFactory.CreateLogger<OpenAIEmbeddingClient>();
        return new OpenAIEmbeddingClient(apiKey, model, logger);
    }

    private static IChatClient CreateOpenAIChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:OpenAI:ApiKey"] 
            ?? configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AI:OpenAI:ApiKey is required");
        var model = configuration["AI:OpenAI:ChatModel"] 
            ?? configuration["OpenAI:ChatModel"] 
            ?? "gpt-4o-mini";
        
        var logger = loggerFactory.CreateLogger<OpenAIChatClient>();
        return new OpenAIChatClient(apiKey, model, logger);
    }

    private static IEmbeddingClient CreateGoogleAIEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:Google:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Google:ApiKey is required");
        var model = configuration["AI:Google:EmbeddingModel"] ?? "text-embedding-004";
        
        var logger = loggerFactory.CreateLogger<GoogleAIEmbeddingClient>();
        return new GoogleAIEmbeddingClient(apiKey, model, logger);
    }

    private static IChatClient CreateGoogleAIChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:Google:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Google:ApiKey is required");
        var model = configuration["AI:Google:ChatModel"] ?? "gemini-pro";
        
        var logger = loggerFactory.CreateLogger<GoogleAIChatClient>();
        return new GoogleAIChatClient(apiKey, model, logger);
    }

    private static IEmbeddingClient CreateMicrosoftAIEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var endpoint = configuration["AI:Microsoft:Endpoint"] 
            ?? throw new InvalidOperationException("AI:Microsoft:Endpoint is required");
        var apiKey = configuration["AI:Microsoft:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Microsoft:ApiKey is required");
        var model = configuration["AI:Microsoft:EmbeddingModel"] ?? "text-embedding-ada-002";
        
        var logger = loggerFactory.CreateLogger<MicrosoftAIEmbeddingClient>();
        return new MicrosoftAIEmbeddingClient(endpoint, apiKey, model, logger);
    }

    private static IChatClient CreateMicrosoftAIChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var endpoint = configuration["AI:Microsoft:Endpoint"] 
            ?? throw new InvalidOperationException("AI:Microsoft:Endpoint is required");
        var apiKey = configuration["AI:Microsoft:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Microsoft:ApiKey is required");
        var model = configuration["AI:Microsoft:ChatModel"] ?? "gpt-4";
        
        var logger = loggerFactory.CreateLogger<MicrosoftAIChatClient>();
        return new MicrosoftAIChatClient(endpoint, apiKey, model, logger);
    }

    private static IEmbeddingClient CreateAmazonAIEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var region = configuration["AI:Amazon:Region"] ?? "us-east-1";
        var accessKey = configuration["AI:Amazon:AccessKey"] 
            ?? throw new InvalidOperationException("AI:Amazon:AccessKey is required");
        var secretKey = configuration["AI:Amazon:SecretKey"] 
            ?? throw new InvalidOperationException("AI:Amazon:SecretKey is required");
        var model = configuration["AI:Amazon:EmbeddingModel"] ?? "amazon.titan-embed-text-v1";
        
        var logger = loggerFactory.CreateLogger<AmazonAIEmbeddingClient>();
        return new AmazonAIEmbeddingClient(region, accessKey, secretKey, model, logger);
    }

    private static IChatClient CreateAmazonAIChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var region = configuration["AI:Amazon:Region"] ?? "us-east-1";
        var accessKey = configuration["AI:Amazon:AccessKey"] 
            ?? throw new InvalidOperationException("AI:Amazon:AccessKey is required");
        var secretKey = configuration["AI:Amazon:SecretKey"] 
            ?? throw new InvalidOperationException("AI:Amazon:SecretKey is required");
        var model = configuration["AI:Amazon:ChatModel"] ?? "anthropic.claude-3-sonnet-20240229-v1:0";
        
        var logger = loggerFactory.CreateLogger<AmazonAIChatClient>();
        return new AmazonAIChatClient(region, accessKey, secretKey, model, logger);
    }

    private static IEmbeddingClient CreateAnthropicEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        // Anthropic doesn't have an embedding API, so we'll use a placeholder or throw
        throw new NotSupportedException("Anthropic does not provide an embedding API. Please use a different provider for embeddings.");
    }

    private static IChatClient CreateAnthropicChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:Anthropic:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Anthropic:ApiKey is required");
        var model = configuration["AI:Anthropic:ChatModel"] ?? "claude-3-sonnet-20240229";
        
        var logger = loggerFactory.CreateLogger<AnthropicChatClient>();
        return new AnthropicChatClient(apiKey, model, logger);
    }

    private static IEmbeddingClient CreateMistralAIEmbedding(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:Mistral:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Mistral:ApiKey is required");
        var model = configuration["AI:Mistral:EmbeddingModel"] ?? "mistral-embed";
        
        var logger = loggerFactory.CreateLogger<MistralAIEmbeddingClient>();
        return new MistralAIEmbeddingClient(apiKey, model, logger);
    }

    private static IChatClient CreateMistralAIChat(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        var apiKey = configuration["AI:Mistral:ApiKey"] 
            ?? throw new InvalidOperationException("AI:Mistral:ApiKey is required");
        var model = configuration["AI:Mistral:ChatModel"] ?? "mistral-small";
        
        var logger = loggerFactory.CreateLogger<MistralAIChatClient>();
        return new MistralAIChatClient(apiKey, model, logger);
    }
}
