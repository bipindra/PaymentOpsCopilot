using Microsoft.AspNetCore.Mvc;
using PaymentOps.Backend.Application.Services;
using PaymentOps.Backend.DTOs;

namespace PaymentOps.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController : ControllerBase
{
    private readonly IngestService _ingestService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IngestController> _logger;
    private readonly long _maxFileSizeBytes;

    public IngestController(
        IngestService ingestService, 
        IConfiguration configuration,
        ILogger<IngestController> logger)
    {
        _ingestService = ingestService;
        _configuration = configuration;
        _logger = logger;
        _maxFileSizeBytes = configuration.GetValue<long>("RAG:MaxFileSizeBytes", 10485760); // 10MB default
    }

    [HttpPost("text")]
    public async Task<IActionResult> IngestText([FromBody] IngestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocName) || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "DocName and Text are required" });
        }

        // Check text size (approximate byte count)
        var textSizeBytes = System.Text.Encoding.UTF8.GetByteCount(request.Text);
        if (textSizeBytes > _maxFileSizeBytes)
        {
            return BadRequest(new { error = $"Text is too large ({textSizeBytes} bytes, max {_maxFileSizeBytes} bytes)" });
        }

        try
        {
            var document = await _ingestService.IngestTextAsync(
                request.DocName,
                request.Text,
                cancellationToken: cancellationToken);

            return Ok(new
            {
                documentId = document.Id,
                docName = document.Name,
                chunkCount = document.ChunkCount,
                createdUtc = document.CreatedUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting text");
            return StatusCode(500, new { error = "Failed to ingest document" });
        }
    }

    [HttpPost("files")]
    public async Task<IActionResult> IngestFiles(IFormFileCollection files, CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { error = "No files provided" });
        }

        var results = new List<object>();
        var tempFiles = new List<string>();

        try
        {
            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                // Check file size before processing
                if (file.Length > _maxFileSizeBytes)
                {
                    results.Add(new
                    {
                        fileName = file.FileName,
                        error = $"File is too large ({file.Length} bytes, max {_maxFileSizeBytes} bytes)"
                    });
                    continue;
                }

                var allowedExtensions = new[] { ".md", ".txt" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    results.Add(new
                    {
                        fileName = file.FileName,
                        error = "Only .md and .txt files are allowed"
                    });
                    continue;
                }

                var tempPath = Path.GetTempFileName();
                tempFiles.Add(tempPath);

                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var text = await System.IO.File.ReadAllTextAsync(tempPath, cancellationToken);
                var document = await _ingestService.IngestTextAsync(
                    file.FileName,
                    text,
                    file.FileName,
                    cancellationToken);

                results.Add(new
                {
                    fileName = file.FileName,
                    documentId = document.Id,
                    chunkCount = document.ChunkCount,
                    createdUtc = document.CreatedUtc
                });
            }

            return Ok(new { results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting files");
            return StatusCode(500, new { error = "Failed to ingest files" });
        }
        finally
        {
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (System.IO.File.Exists(tempFile))
                        System.IO.File.Delete(tempFile);
                }
                catch { }
            }
        }
    }

    [HttpPost("samples")]
    public async Task<IActionResult> IngestSamples([FromBody] IngestSamplesRequest request, CancellationToken cancellationToken)
    {
        var folderPath = request.FolderPath ?? "samples/runbooks";
        
        if (!Directory.Exists(folderPath))
        {
            return BadRequest(new { error = $"Folder not found: {folderPath}" });
        }

        try
        {
            var files = Directory.GetFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(folderPath, "*.txt", SearchOption.TopDirectoryOnly))
                .ToList();

            if (files.Count == 0)
            {
                return BadRequest(new { error = $"No .md or .txt files found in {folderPath}" });
            }

            var documents = await _ingestService.IngestFilesAsync(files, cancellationToken);

            return Ok(new
            {
                ingested = documents.Count,
                documents = documents.Select(d => new
                {
                    documentId = d.Id,
                    docName = d.Name,
                    chunkCount = d.ChunkCount,
                    createdUtc = d.CreatedUtc
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting samples");
            return StatusCode(500, new { error = "Failed to ingest samples" });
        }
    }
}

public class IngestSamplesRequest
{
    public string? FolderPath { get; set; }
}
