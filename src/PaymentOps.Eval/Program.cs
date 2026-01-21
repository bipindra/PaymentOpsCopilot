using Microsoft.Extensions.Logging;
using PaymentOps.Eval;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
var evalJsonPath = args.Length > 1 ? args[1] : "../../../samples/eval/eval.json";
var reportPath = args.Length > 2 ? args[2] : "../../../samples/eval/report.md";

// Resolve relative paths
evalJsonPath = Path.GetFullPath(evalJsonPath);
reportPath = Path.GetFullPath(reportPath);

Console.WriteLine($"Evaluation Harness");
Console.WriteLine($"Base URL: {baseUrl}");
Console.WriteLine($"Eval JSON: {evalJsonPath}");
Console.WriteLine($"Report: {reportPath}");
Console.WriteLine();

var logger = new ConsoleLogger();
var evaluator = new Evaluator(baseUrl, logger);

try
{
    var report = await evaluator.EvaluateAsync(evalJsonPath);
    
    var markdown = evaluator.GenerateMarkdownReport(report);
    
    // Ensure directory exists
    var reportDir = Path.GetDirectoryName(reportPath);
    if (!string.IsNullOrEmpty(reportDir) && !Directory.Exists(reportDir))
    {
        Directory.CreateDirectory(reportDir);
    }
    
    await File.WriteAllTextAsync(reportPath, markdown);
    
    Console.WriteLine();
    Console.WriteLine("Evaluation Complete!");
    Console.WriteLine($"Passed: {report.PassedTests}/{report.TotalTests}");
    Console.WriteLine($"Report written to: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}

class ConsoleLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine(formatter(state, exception));
    }
}
