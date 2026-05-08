using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Keywords;
using BitacoraTech.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Infrastructure.KeywordResearch;

public sealed class GoogleTrendsSource(HttpClient httpClient, IOptions<KeywordResearchOptions> options) : IKeywordTrendSource
{
    public string Name => "google-trends";

    public async Task<IReadOnlyCollection<TrendKeywordCandidate>> GetCandidatesAsync(KeywordResearchContext context, CancellationToken cancellationToken)
    {
        var country = string.IsNullOrWhiteSpace(context.Country) ? options.Value.DefaultCountry : context.Country;
        var uri = new Uri($"https://trends.google.com/trends/trendingsearches/daily/rss?geo={Uri.EscapeDataString(country)}");
        using var stream = await httpClient.GetStreamAsync(uri, cancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var titles = document.Descendants().Where(x => x.Name.LocalName == "item")
            .Select(x => x.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Take(options.Value.MaxCandidatesPerSource)
            .Select(x => new TrendKeywordCandidate(WebUtility.HtmlDecode(x!).Trim(), Name, 1, 95, DateTimeOffset.UtcNow))
            .ToArray();

        return titles;
    }
}

public sealed class RedditTrendSource(HttpClient httpClient, IOptions<KeywordResearchOptions> options) : IKeywordTrendSource
{
    public string Name => "reddit";

    public async Task<IReadOnlyCollection<TrendKeywordCandidate>> GetCandidatesAsync(KeywordResearchContext context, CancellationToken cancellationToken)
    {
        var subreddits = context.Subreddits.Count > 0 ? context.Subreddits : options.Value.RedditSubreddits;
        var candidates = new List<TrendKeywordCandidate>();
        httpClient.DefaultRequestHeaders.UserAgent.Clear();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BitacoraTechKeywordResearch", "1.0"));

        foreach (var subreddit in subreddits.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var query = string.IsNullOrWhiteSpace(context.SeedKeyword) ? "hot" : $"search.json?q={Uri.EscapeDataString(context.SeedKeyword)}&restrict_sr=1&sort=hot&t=week";
            var path = query == "hot" ? $"r/{subreddit.Trim('/')}/hot.json?limit=15" : $"r/{subreddit.Trim('/')}/{query}&limit=15";
            using var response = await httpClient.GetAsync(new Uri($"https://www.reddit.com/{path}"), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var posts = document.RootElement.GetProperty("data").GetProperty("children").EnumerateArray();
            foreach (var post in posts)
            {
                var data = post.GetProperty("data");
                var title = data.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var score = data.TryGetProperty("score", out var scoreProperty) ? Math.Max(1, scoreProperty.GetInt32()) : 1;
                candidates.Add(new TrendKeywordCandidate(title, Name, 1, Math.Clamp(score / 10m, 1, 100), DateTimeOffset.UtcNow));
            }
        }

        return candidates.Take(options.Value.MaxCandidatesPerSource).ToArray();
    }
}

public sealed class RssTrendSource(HttpClient httpClient, IOptions<KeywordResearchOptions> options) : IKeywordTrendSource
{
    public string Name => "rss";

    public async Task<IReadOnlyCollection<TrendKeywordCandidate>> GetCandidatesAsync(KeywordResearchContext context, CancellationToken cancellationToken)
    {
        var feeds = context.RssFeeds.Count > 0 ? context.RssFeeds : options.Value.RssFeeds;
        var candidates = new List<TrendKeywordCandidate>();

        foreach (var feed in feeds.Where(x => Uri.TryCreate(x, UriKind.Absolute, out _)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var stream = await httpClient.GetStreamAsync(feed, cancellationToken);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            var items = document.Descendants().Where(x => x.Name.LocalName is "item" or "entry");
            foreach (var item in items.Take(options.Value.MaxCandidatesPerSource))
            {
                var title = item.Elements().FirstOrDefault(x => x.Name.LocalName == "title")?.Value;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    candidates.Add(new TrendKeywordCandidate(WebUtility.HtmlDecode(title).Trim(), Name, 1, 60, DateTimeOffset.UtcNow));
                }
            }
        }

        return candidates.Take(options.Value.MaxCandidatesPerSource).ToArray();
    }
}

public sealed partial class KeywordResearchRunner(
    IEnumerable<IKeywordTrendSource> sources,
    IKeywordRepository keywords,
    IKeywordResearchRunRepository researchRuns,
    IUnitOfWork unitOfWork) : IKeywordResearchRunner
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "de", "del", "el", "en", "for", "from", "how", "in", "is", "la", "las", "los", "of", "on", "or", "para", "por", "que", "the", "to", "un", "una", "with", "y"
    };

    public async Task<KeywordResearchResult> RunAsync(KeywordResearchContext context, CancellationToken cancellationToken)
    {
        var researchRun = new KeywordResearchRun(Guid.NewGuid(), context.TenantId, context.SiteId, context.SeedKeyword);
        await researchRuns.AddAsync(researchRun, cancellationToken);

        var rawCandidates = new List<TrendKeywordCandidate>();
        foreach (var source in sources)
        {
            try
            {
                rawCandidates.AddRange(await source.GetCandidatesAsync(context, cancellationToken));
            }
            catch (HttpRequestException)
            {
                continue;
            }
        }

        rawCandidates.Add(new TrendKeywordCandidate(context.SeedKeyword, "seed", 1, 45, DateTimeOffset.UtcNow));
        var scored = ScoreAndCluster(rawCandidates, context.SeedKeyword);

        foreach (var candidate in scored)
        {
            var existing = await keywords.GetBySiteAndTextAsync(context.SiteId, candidate.Text, cancellationToken);
            if (existing is null)
            {
                var keyword = new Keyword(
                    Guid.NewGuid(),
                    context.TenantId,
                    context.SiteId,
                    researchRun.Id,
                    candidate.Text,
                    candidate.SearchVolume,
                    candidate.Difficulty,
                    candidate.TrendScore,
                    candidate.OpportunityScore,
                    candidate.Mentions,
                    candidate.Source,
                    candidate.Cluster,
                    candidate.Intent,
                    candidate.SeenAt);
                researchRun.AddKeyword(keyword);
                await keywords.AddAsync(keyword, cancellationToken);
            }
            else
            {
                existing.RefreshFromResearch(
                    researchRun.Id,
                    candidate.SearchVolume,
                    candidate.Difficulty,
                    candidate.TrendScore,
                    candidate.OpportunityScore,
                    candidate.Mentions,
                    candidate.Source,
                    candidate.Cluster,
                    candidate.Intent,
                    candidate.SeenAt);
            }
        }

        researchRun.MarkCompleted(DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new KeywordResearchResult(researchRun.Id, scored);
    }

    private static IReadOnlyCollection<ScoredKeywordCandidate> ScoreAndCluster(IEnumerable<TrendKeywordCandidate> candidates, string seed)
    {
        return candidates
            .SelectMany(candidate => ExtractPhrases(candidate).Select(phrase => candidate with { Text = phrase }))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            .GroupBy(candidate => Normalize(candidate.Text), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var best = group.OrderByDescending(x => x.SourceScore).First();
                var mentions = group.Sum(x => x.Mentions);
                var trendScore = Math.Clamp(group.Average(x => x.SourceScore) + Math.Min(25, mentions * 3), 0, 100);
                var searchVolume = (int)Math.Round(mentions * 90 + trendScore * 12);
                var difficulty = EstimateDifficulty(best.Text, trendScore);
                var opportunity = Math.Clamp((trendScore * 0.55m) + ((100 - difficulty) * 0.35m) + Math.Min(10, mentions), 0, 100);
                return new ScoredKeywordCandidate(
                    best.Text,
                    string.Join("+", group.Select(x => x.Source).Distinct(StringComparer.OrdinalIgnoreCase)),
                    searchVolume,
                    difficulty,
                    Math.Round(trendScore, 2),
                    Math.Round(opportunity, 2),
                    mentions,
                    BuildCluster(best.Text, seed),
                    InferIntent(best.Text),
                    group.Max(x => x.SeenAt));
            })
            .OrderByDescending(x => x.OpportunityScore)
            .ThenByDescending(x => x.TrendScore)
            .Take(50)
            .ToArray();
    }

    private static IEnumerable<string> ExtractPhrases(TrendKeywordCandidate candidate)
    {
        var text = NonKeywordCharacters().Replace(candidate.Text.ToLowerInvariant(), " ");
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .Take(10)
            .ToArray();

        if (words.Length == 0)
        {
            yield break;
        }

        yield return string.Join(' ', words.Take(Math.Min(4, words.Length)));
        for (var index = 0; index < words.Length - 1; index++)
        {
            yield return $"{words[index]} {words[index + 1]}";
        }
    }

    private static decimal EstimateDifficulty(string text, decimal trendScore)
    {
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var longTailDiscount = Math.Min(25, Math.Max(0, wordCount - 2) * 8);
        return Math.Clamp(35 + trendScore * 0.45m - longTailDiscount, 5, 95);
    }

    private static string BuildCluster(string text, string seed)
    {
        var seedToken = seed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        var firstToken = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return !string.IsNullOrWhiteSpace(seedToken) && text.Contains(seedToken, StringComparison.OrdinalIgnoreCase)
            ? seedToken.ToLowerInvariant()
            : firstToken?.ToLowerInvariant() ?? "general";
    }

    private static string InferIntent(string text)
    {
        if (text.Contains("best", StringComparison.OrdinalIgnoreCase) || text.Contains("review", StringComparison.OrdinalIgnoreCase))
        {
            return "commercial";
        }

        if (text.Contains("buy", StringComparison.OrdinalIgnoreCase) || text.Contains("price", StringComparison.OrdinalIgnoreCase))
        {
            return "transactional";
        }

        return text.Contains("how", StringComparison.OrdinalIgnoreCase) || text.Contains("what", StringComparison.OrdinalIgnoreCase)
            ? "informational"
            : "trend";
    }

    private static string Normalize(string text) => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    [GeneratedRegex("[^a-z0-9áéíóúñü ]+", RegexOptions.IgnoreCase)]
    private static partial Regex NonKeywordCharacters();
}
