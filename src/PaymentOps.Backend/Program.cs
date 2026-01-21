using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Application.Services;
using PaymentOps.Backend.Infrastructure;
using PaymentOps.Backend.Infrastructure.Guardrails;
using PaymentOps.Backend.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (builder.Environment.IsDevelopment())
{
    // Enables: dotnet user-secrets set "OpenAI:ApiKey" "..."
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            // Safe dev default if not configured.
            policy.WithOrigins("http://localhost:4200");
        }

        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            // Allow the frontend to read the correlation id we set on responses.
            .WithExposedHeaders("X-Correlation-ID");
    });
});


// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "PaymentOps.Backend"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Configuration
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
    ?? builder.Configuration["OpenAI:ApiKey"] 
    ?? throw new InvalidOperationException(
        "OpenAI API key is required. Set OPENAI_API_KEY env var or configure OpenAI:ApiKey (recommended: dotnet user-secrets set \"OpenAI:ApiKey\" \"...\").");

// Register application services
builder.Services.AddSingleton<IEmbeddingClient>(sp =>
    new OpenAIEmbeddingClient(
        openAiApiKey,
        builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small",
        sp.GetRequiredService<ILogger<OpenAIEmbeddingClient>>()));

builder.Services.AddSingleton<IChatClient>(sp =>
    new OpenAIChatClient(
        openAiApiKey,
        builder.Configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini",
        sp.GetRequiredService<ILogger<OpenAIChatClient>>()));

// Register vector store using factory
builder.Services.AddSingleton<IVectorStore>(sp =>
    VectorStoreFactory.Create(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILoggerFactory>()));

builder.Services.AddSingleton<PromptInjectionDetector>();
builder.Services.AddScoped<ChunkingService>();
builder.Services.AddScoped<IngestService>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddScoped<AskService>();

var app = builder.Build();

// Middleware
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("DefaultCors");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// Initialize vector store
using (var scope = app.Services.CreateScope())
{
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    try
    {
        await vectorStore.InitializeAsync();
        Log.Information("Vector store initialized successfully");
    }
    catch (Exception ex)
    {
        var provider = app.Configuration["VectorStore:Provider"] ?? "Qdrant";
        Log.Error(ex, "Failed to initialize vector store (Provider: {Provider}). Make sure the vector database is running and configured correctly.", provider);
    }
}

Log.Information("PaymentOps.Backend starting on {Urls}", string.Join(", ", app.Urls));

app.Run();
