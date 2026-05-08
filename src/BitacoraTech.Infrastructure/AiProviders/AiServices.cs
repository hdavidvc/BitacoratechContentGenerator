using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Ai;
using BitacoraTech.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Infrastructure.AiProviders;

public sealed class FallbackAiGateway(
    IEnumerable<IAiProvider> providers,
    IAiCostTracker costTracker,
    IOptions<AiOptions> options,
    ILogger<FallbackAiGateway> logger) : IAiGateway
{
    private readonly RetryPolicyOptions retry = options.Value.Retry;

    public async Task<string> CompleteAsync(string featureArea, string prompt, CancellationToken cancellationToken)
    {
        var request = new AiCompletionRequest(featureArea, prompt);
        var failures = new List<string>();

        foreach (var provider in providers.Where(x => x.IsEnabled).OrderBy(x => x.Priority))
        {
            try
            {
                var result = await ExecuteWithRetryAsync(provider, request, cancellationToken);
                await costTracker.TrackAsync(
                    result.Provider,
                    result.Model,
                    result.PromptTokens,
                    result.CompletionTokens,
                    result.EstimatedCost,
                    request.FeatureArea,
                    request.RelatedEntityId,
                    cancellationToken);

                return result.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add($"{provider.Name}: {ex.Message}");
                logger.LogWarning(ex, "AI provider {Provider} failed for feature area {FeatureArea}. Falling back.", provider.Name, featureArea);
            }
        }

        throw new InvalidOperationException($"No AI provider completed the request. Failures: {string.Join("; ", failures)}");
    }

    private async Task<AiCompletionResult> ExecuteWithRetryAsync(IAiProvider provider, AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, retry.MaxAttempts);
        var delay = Math.Max(50, retry.InitialDelayMilliseconds);
        var maxDelay = Math.Max(delay, retry.MaxDelayMilliseconds);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await provider.CompleteAsync(request, cancellationToken);
            }
            catch (Exception ex) when (attempt < attempts && IsTransient(ex))
            {
                logger.LogWarning(ex, "Transient AI failure from {Provider} on attempt {Attempt}/{Attempts}.", provider.Name, attempt, attempts);
                await Task.Delay(delay, cancellationToken);
                delay = Math.Min(delay * 2, maxDelay);
            }
        }

        return await provider.CompleteAsync(request, cancellationToken);
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException httpRequestException
            && (httpRequestException.StatusCode is null
                || httpRequestException.StatusCode == HttpStatusCode.TooManyRequests
                || (int)httpRequestException.StatusCode >= 500);
    }
}

public sealed class SqlAiCostTracker(
    ICurrentUserContext currentUser,
    IAiUsageRepository usageRecords,
    IUnitOfWork unitOfWork) : IAiCostTracker
{
    public async Task TrackAsync(string provider, string model, int promptTokens, int completionTokens, decimal estimatedCost, string featureArea, Guid? relatedEntityId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? Guid.Empty;
        var usageRecord = new AiUsageRecord(Guid.NewGuid(), tenantId, provider, model, promptTokens, completionTokens, estimatedCost, featureArea, relatedEntityId);
        await usageRecords.AddAsync(usageRecord, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PromptTemplateService : IPromptTemplateService
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["article.generate"] = "Write an SEO article about {{keyword}} for {{audience}}. Use semantic headings, natural internal-link placeholders, and a human review friendly tone.",
        ["keyword.research"] = "Find SEO keyword opportunities for {{seed}} in {{market}}. Return intent, difficulty, and content angle.",
        ["seo.analyze"] = "Analyze this HTML for semantic SEO quality around {{keyword}} and return prioritized recommendations: {{html}}"
    };

    public Task<string> RenderAsync(string key, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        var template = Defaults.TryGetValue(key, out var value) ? value : key;
        var rendered = values.Aggregate(template, (current, pair) => current.Replace($"{{{{{pair.Key}}}}}", pair.Value, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(rendered);
    }
}

public abstract class AiProviderBase(
    string providerName,
    IHttpClientFactory httpClientFactory,
    IOptions<AiOptions> options) : IAiProvider
{
    private readonly AiProviderOptions providerOptions = options.Value.GetProvider(providerName);

    public string Name => providerName;
    public bool IsEnabled => providerOptions.Enabled && !string.IsNullOrWhiteSpace(providerOptions.ApiKey);
    public int Priority => providerOptions.Priority;

    protected AiProviderOptions Options => providerOptions;
    protected HttpClient CreateClient() => httpClientFactory.CreateClient($"ai-{Name}");

    public abstract Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken);

    protected AiCompletionResult BuildResult(string text, AiCompletionRequest request, int? promptTokens, int? completionTokens)
    {
        var actualPromptTokens = Math.Max(0, promptTokens ?? EstimateTokens(request.Prompt));
        var actualCompletionTokens = Math.Max(0, completionTokens ?? EstimateTokens(text));
        var cost = ((actualPromptTokens / 1_000_000m) * Options.PromptTokenPricePerMillion)
            + ((actualCompletionTokens / 1_000_000m) * Options.CompletionTokenPricePerMillion);

        return new AiCompletionResult(Name, Options.Model, text, actualPromptTokens, actualCompletionTokens, Math.Round(cost, 6));
    }

    protected int MaxTokens(AiCompletionRequest request) => request.MaxTokens ?? Options.MaxTokens;

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    protected static int? GetInt32(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value) ? value : null;
    }

    private static int EstimateTokens(string text)
    {
        return Math.Max(1, (int)Math.Ceiling(text.Length / 4m));
    }
}

public sealed class GeminiProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options)
    : AiProviderBase("Gemini", httpClientFactory, options)
{
    public override async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds);

        var endpoint = $"{Options.BaseUrl.TrimEnd('/')}/v1beta/models/{Options.Model}:generateContent?key={Uri.EscapeDataString(Options.ApiKey)}";
        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = request.Prompt } } }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = MaxTokens(request)
            }
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var text = root.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            ?? string.Empty;

        return BuildResult(
            text,
            request,
            GetInt32(root, "usageMetadata", "promptTokenCount"),
            GetInt32(root, "usageMetadata", "candidatesTokenCount"));
    }
}

public sealed class OpenAiProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options)
    : AiProviderBase("OpenAI", httpClientFactory, options)
{
    public override async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Options.ApiKey);

        var endpoint = $"{Options.BaseUrl.TrimEnd('/')}/v1/chat/completions";
        var payload = new
        {
            model = Options.Model,
            temperature = request.Temperature,
            max_tokens = MaxTokens(request),
            messages = new[]
            {
                new { role = "system", content = "You are BitacoraTech's SEO automation assistant. Respect human review before publishing." },
                new { role = "user", content = request.Prompt }
            }
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? string.Empty;

        return BuildResult(
            text,
            request,
            GetInt32(root, "usage", "prompt_tokens"),
            GetInt32(root, "usage", "completion_tokens"));
    }
}

public sealed class ClaudeProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options)
    : AiProviderBase("Claude", httpClientFactory, options)
{
    public override async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("x-api-key", Options.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var endpoint = $"{Options.BaseUrl.TrimEnd('/')}/v1/messages";
        var payload = new
        {
            model = Options.Model,
            max_tokens = MaxTokens(request),
            temperature = request.Temperature,
            system = "You are BitacoraTech's SEO automation assistant. Respect human review before publishing.",
            messages = new[]
            {
                new { role = "user", content = request.Prompt }
            }
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var text = root.GetProperty("content").EnumerateArray()
            .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
            .Select(x => x.GetProperty("text").GetString())
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;

        return BuildResult(
            text,
            request,
            GetInt32(root, "usage", "input_tokens"),
            GetInt32(root, "usage", "output_tokens"));
    }
}
