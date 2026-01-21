using Microsoft.AspNetCore.Mvc;
using PaymentOps.Backend.Application.Interfaces;

namespace PaymentOps.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<SourcesController> _logger;

    public SourcesController(IVectorStore vectorStore, ILogger<SourcesController> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken)
    {
        try
        {
            var documents = await _vectorStore.GetDocumentsAsync(cancellationToken);
            
            return Ok(documents.Select(d => new
            {
                documentId = d.Id,
                docName = d.Name,
                sourcePath = d.SourcePath,
                chunkCount = d.ChunkCount,
                totalSizeBytes = d.TotalSizeBytes,
                createdUtc = d.CreatedUtc
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sources");
            return StatusCode(500, new { error = "Failed to get sources" });
        }
    }

    [HttpGet("{documentId}")]
    public async Task<IActionResult> GetSource(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _vectorStore.GetDocumentAsync(documentId, cancellationToken);
            
            if (document == null)
            {
                return NotFound(new { error = "Document not found" });
            }

            var chunks = await _vectorStore.GetDocumentChunksAsync(documentId, cancellationToken);

            return Ok(new
            {
                documentId = document.Id,
                docName = document.Name,
                sourcePath = document.SourcePath,
                chunkCount = document.ChunkCount,
                totalSizeBytes = document.TotalSizeBytes,
                createdUtc = document.CreatedUtc,
                chunks = chunks.Select(c => new
                {
                    chunkIndex = c.Index,
                    snippet = c.Snippet,
                    textLength = c.Text.Length
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting source");
            return StatusCode(500, new { error = "Failed to get source" });
        }
    }
}
