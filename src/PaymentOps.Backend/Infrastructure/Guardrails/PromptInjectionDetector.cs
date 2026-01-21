namespace PaymentOps.Backend.Infrastructure.Guardrails;

/// <summary>
/// Simple guardrail to detect likely <b>prompt injection</b> attempts (e.g., "ignore previous instructions").
/// <para>
/// Prompt injection is when user-provided text tries to override system rules or extract hidden prompts/secrets.
/// This detector is intentionally lightweight (keyword-based) and is used to either block severe attempts or
/// tighten grounding for moderate attempts.
/// </para>
/// </summary>
public class PromptInjectionDetector
{
    private static readonly HashSet<string> SuspiciousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ignore previous instructions",
        "forget all previous",
        "system prompt",
        "jailbreak",
        "override",
        "bypass",
        "reveal",
        "show me your",
        "what are your instructions",
        "what is your system prompt",
        "disregard",
        "new instructions",
        "act as",
        "pretend to be",
        "roleplay",
        "simulate"
    };

    public PromptInjectionResult Detect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PromptInjectionResult { IsSafe = true };

        var lowerInput = input.ToLowerInvariant();
        var detectedKeywords = SuspiciousKeywords
            .Where(keyword => lowerInput.Contains(keyword.ToLowerInvariant()))
            .ToList();

        if (detectedKeywords.Any())
        {
            var isSevere = detectedKeywords.Any(k => 
                k.Contains("system prompt") || 
                k.Contains("instructions") || 
                k.Contains("reveal"));

            return new PromptInjectionResult
            {
                IsSafe = false,
                DetectedKeywords = detectedKeywords,
                Severity = isSevere ? InjectionSeverity.Severe : InjectionSeverity.Moderate
            };
        }

        return new PromptInjectionResult { IsSafe = true };
    }
}

public class PromptInjectionResult
{
    public bool IsSafe { get; set; }
    public List<string> DetectedKeywords { get; set; } = new();
    public InjectionSeverity Severity { get; set; }
}

public enum InjectionSeverity
{
    None,
    Moderate,
    Severe
}
