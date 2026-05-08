namespace BitacoraTech.Infrastructure.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public RetryPolicyOptions Retry { get; init; } = new();
    public Dictionary<string, AiProviderOptions> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public AiProviderOptions GetProvider(string name)
    {
        return Providers.TryGetValue(name, out var provider) ? provider : new AiProviderOptions();
    }
}

public sealed class RetryPolicyOptions
{
    public int MaxAttempts { get; init; } = 3;
    public int InitialDelayMilliseconds { get; init; } = 250;
    public int MaxDelayMilliseconds { get; init; } = 2_000;
}

public sealed class AiProviderOptions
{
    public bool Enabled { get; init; }
    public int Priority { get; init; } = 100;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 60;
    public int MaxTokens { get; init; } = 2_000;
    public decimal PromptTokenPricePerMillion { get; init; }
    public decimal CompletionTokenPricePerMillion { get; init; }
}
