using Microsoft.AspNetCore.Mvc;
using PaymentOps.Backend.Application.Services;
using PaymentOps.Backend.DTOs;

namespace PaymentOps.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private readonly AskService _askService;
    private readonly ILogger<AskController> _logger;

    public AskController(AskService askService, ILogger<AskController> logger)
    {
        _askService = askService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "Question is required" });
        }

        try
        {
            var response = await _askService.AskAsync(request.Question, request.TopK, cancellationToken);
            
            var dto = new AskResponseDto
            {
                AnswerMarkdown = response.AnswerMarkdown,
                Citations = response.Citations.Select(c => new CitationDto
                {
                    DocumentName = c.DocumentName,
                    ChunkIndex = c.ChunkIndex,
                    Snippet = c.Snippet,
                    Score = c.Score
                }).ToList(),
                Retrieved = response.Retrieved.Select(r => new CitationDto
                {
                    DocumentName = r.DocumentName,
                    ChunkIndex = r.ChunkIndex,
                    Snippet = r.Snippet,
                    Score = r.Score
                }).ToList(),
                ElapsedMs = response.ElapsedMs,
                TokensUsed = response.TokensUsed
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing question");
            return StatusCode(500, new { error = "Failed to process question" });
        }
    }
}
