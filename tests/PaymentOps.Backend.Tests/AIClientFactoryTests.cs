using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Infrastructure;
using Xunit;

namespace PaymentOps.Backend.Tests;

public class AIClientFactoryTests
{
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public AIClientFactoryTests()
    {
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        var loggerMock = new Mock<ILogger>();
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(loggerMock.Object);
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<Type>()))
            .Returns(loggerMock.Object);
    }

    [Fact]
    public void AIClientFactory_CreatesOpenAIEmbeddingClient_WhenProviderIsOpenAI()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "OpenAI" },
            { "AI:OpenAI:ApiKey", "test-key" },
            { "AI:OpenAI:EmbeddingModel", "text-embedding-3-small" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var client = AIClientFactory.CreateEmbeddingClient(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<OpenAIEmbeddingClient>(client);
    }

    [Fact]
    public void AIClientFactory_CreatesOpenAIChatClient_WhenProviderIsOpenAI()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "OpenAI" },
            { "AI:OpenAI:ApiKey", "test-key" },
            { "AI:OpenAI:ChatModel", "gpt-4o-mini" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var client = AIClientFactory.CreateChatClient(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(client);
        Assert.IsType<OpenAIChatClient>(client);
    }

    [Fact]
    public void AIClientFactory_CreatesGoogleAIClients_WhenProviderIsGoogle()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "Google" },
            { "AI:Google:ApiKey", "test-key" },
            { "AI:Google:EmbeddingModel", "models/embedding-001" },
            { "AI:Google:ChatModel", "models/gemini-pro" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var embeddingClient = AIClientFactory.CreateEmbeddingClient(config, _loggerFactoryMock.Object);
        var chatClient = AIClientFactory.CreateChatClient(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(embeddingClient);
        Assert.IsType<GoogleAIEmbeddingClient>(embeddingClient);
        Assert.NotNull(chatClient);
        Assert.IsType<GoogleAIChatClient>(chatClient);
    }

    [Fact]
    public void AIClientFactory_CreatesMicrosoftAIClients_WhenProviderIsMicrosoft()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "Microsoft" },
            { "AI:Microsoft:Endpoint", "https://test.openai.azure.com" },
            { "AI:Microsoft:ApiKey", "test-key" },
            { "AI:Microsoft:EmbeddingDeploymentName", "text-embedding-ada-002" },
            { "AI:Microsoft:ChatDeploymentName", "gpt-35-turbo" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var embeddingClient = AIClientFactory.CreateEmbeddingClient(config, _loggerFactoryMock.Object);
        var chatClient = AIClientFactory.CreateChatClient(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(embeddingClient);
        Assert.IsType<MicrosoftAIEmbeddingClient>(embeddingClient);
        Assert.NotNull(chatClient);
        Assert.IsType<MicrosoftAIChatClient>(chatClient);
    }

    [Fact]
    public void AIClientFactory_CreatesAnthropicChatClient_WhenProviderIsAnthropic()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "Anthropic" },
            { "AI:Anthropic:ApiKey", "test-key" },
            { "AI:Anthropic:ChatModel", "claude-3-opus-20240229" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        var chatClient = AIClientFactory.CreateChatClient(config, _loggerFactoryMock.Object);

        // Assert
        Assert.NotNull(chatClient);
        Assert.IsType<AnthropicChatClient>(chatClient);
    }

    [Fact]
    public void AIClientFactory_ThrowsNotSupportedException_WhenAnthropicEmbeddingRequested()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "Anthropic" },
            { "AI:Anthropic:ApiKey", "test-key" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<NotSupportedException>(() =>
            AIClientFactory.CreateEmbeddingClient(config, _loggerFactoryMock.Object));
        Assert.Contains("Anthropic does not provide an embedding API", exception.Message);
    }

    [Fact]
    public void AIClientFactory_ThrowsException_WhenProviderIsUnknown()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "AI:Provider", "UnknownProvider" }
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            AIClientFactory.CreateEmbeddingClient(config, _loggerFactoryMock.Object));
        Assert.Throws<InvalidOperationException>(() =>
            AIClientFactory.CreateChatClient(config, _loggerFactoryMock.Object));
    }
}
