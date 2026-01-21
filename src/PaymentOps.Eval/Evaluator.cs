using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PaymentOps.Eval;

public class Evaluator
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger _logger;

    public Evaluator(string baseUrl, ILogger logger)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _logger = logger;
    }

    public async Task<EvaluationReport> EvaluateAsync(string evalJsonPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(evalJsonPath))
        {
            throw new FileNotFoundException($"Eval file not found: {evalJsonPath}");
        }

        var json = await File.ReadAllTextAsync(evalJsonPath, cancellationToken);
        var testCases = JsonSerializer.Deserialize<List<EvalTestCase>>(json)
            ?? throw new InvalidOperationException("Failed to parse eval.json");

        _logger.LogInformation("Loaded {Count} test cases", testCases.Count);

        var results = new List<TestCaseResult>();

        foreach (var testCase in testCases)
        {
            _logger.LogInformation("Testing: {Id} - {Question}", testCase.Id, testCase.Question);
            
            var result = await EvaluateTestCaseAsync(testCase, cancellationToken);
            results.Add(result);
            
            _logger.LogInformation("Result: {Passed} - {Reason}", result.Passed, result.Reason);
        }

        return new EvaluationReport
        {
            TestCases = results,
            TotalTests = results.Count,
            PassedTests = results.Count(r => r.Passed),
            FailedTests = results.Count(r => !r.Passed),
            GeneratedAt = DateTime.UtcNow
        };
    }

    private async Task<TestCaseResult> EvaluateTestCaseAsync(EvalTestCase testCase, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                question = testCase.Question,
                topK = 5
            };

            var response = await _httpClient.PostAsJsonAsync("/api/ask", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    Passed = false,
                    Reason = $"API error: {error}"
                };
            }

            var askResponse = await response.Content.ReadFromJsonAsync<AskResponse>(cancellationToken: cancellationToken);
            
            if (askResponse == null)
            {
                return new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    Passed = false,
                    Reason = "Null response from API"
                };
            }

            var answer = askResponse.AnswerMarkdown ?? "";
            var citations = askResponse.Citations ?? new List<CitationDto>();

            // Check if answer contains "I don't know" when retrieval is empty
            if (askResponse.Retrieved == null || askResponse.Retrieved.Count == 0)
            {
                if (answer.Contains("I don't know", StringComparison.OrdinalIgnoreCase))
                {
                    return new TestCaseResult
                    {
                        TestCaseId = testCase.Id,
                        Passed = true,
                        Reason = "Correctly returned 'I don't know' for empty retrieval"
                    };
                }
                else
                {
                    return new TestCaseResult
                    {
                        TestCaseId = testCase.Id,
                        Passed = false,
                        Reason = "Empty retrieval but answer doesn't contain 'I don't know'"
                    };
                }
            }

            // Check for required citations
            if (testCase.MustCite && citations.Count == 0)
            {
                return new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    Passed = false,
                    Reason = "MustCite=true but no citations found"
                };
            }

            // Check for required keywords
            if (testCase.MustContain != null && testCase.MustContain.Count > 0)
            {
                var missingKeywords = testCase.MustContain
                    .Where(keyword => !answer.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (missingKeywords.Any())
                {
                    return new TestCaseResult
                    {
                        TestCaseId = testCase.Id,
                        Passed = false,
                        Reason = $"Missing required keywords: {string.Join(", ", missingKeywords)}"
                    };
                }
            }

            // Check citation format
            if (citations.Count > 0)
            {
                var citationPattern = @"\[([^\]]+):(\d+)\]";
                var matches = Regex.Matches(answer, citationPattern);
                
                if (matches.Count == 0)
                {
                    return new TestCaseResult
                    {
                        TestCaseId = testCase.Id,
                        Passed = false,
                        Reason = "Citations exist but not found in answer text"
                    };
                }
            }

            return new TestCaseResult
            {
                TestCaseId = testCase.Id,
                Passed = true,
                Reason = "All checks passed"
            };
        }
        catch (Exception ex)
        {
            return new TestCaseResult
            {
                TestCaseId = testCase.Id,
                Passed = false,
                Reason = $"Exception: {ex.Message}"
            };
        }
    }

    public string GenerateMarkdownReport(EvaluationReport report)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Evaluation Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine($"**Total Tests:** {report.TotalTests}");
        sb.AppendLine($"**Passed:** {report.PassedTests} ✅");
        sb.AppendLine($"**Failed:** {report.FailedTests} ❌");
        sb.AppendLine($"**Pass Rate:** {(report.TotalTests > 0 ? (report.PassedTests * 100.0 / report.TotalTests):0):F1}%");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var result in report.TestCases)
        {
            var status = result.Passed ? "✅ PASS" : "❌ FAIL";
            sb.AppendLine($"## {result.TestCaseId} - {status}");
            sb.AppendLine();
            sb.AppendLine($"**Reason:** {result.Reason}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public class EvalTestCase
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public List<string>? MustContain { get; set; }
    public bool MustCite { get; set; }
}

public class TestCaseResult
{
    public string TestCaseId { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class EvaluationReport
{
    public List<TestCaseResult> TestCases { get; set; } = new();
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class AskResponse
{
    public string? AnswerMarkdown { get; set; }
    public List<CitationDto>? Citations { get; set; }
    public List<CitationDto>? Retrieved { get; set; }
    public long ElapsedMs { get; set; }
    public int? TokensUsed { get; set; }
}

public class CitationDto
{
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public float? Score { get; set; }
}
