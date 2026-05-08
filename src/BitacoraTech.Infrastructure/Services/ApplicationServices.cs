using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Articles;
using BitacoraTech.Contracts.Common;
using BitacoraTech.Contracts.Keywords;
using BitacoraTech.Contracts.Seo;
using BitacoraTech.Contracts.Sites;
using BitacoraTech.Domain.Ai;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Keywords;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Hangfire;
using BitacoraTech.Infrastructure.Security;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitacoraTech.Infrastructure.Services;

public sealed class KeywordResearchService(
    ICurrentUserContext currentUser,
    IKeywordRepository keywords,
    IKeywordResearchRunner researchRunner,
    IOptions<KeywordResearchOptions> options,
    IJobScheduler jobs) : IKeywordResearchService
{
    public async Task<IReadOnlyCollection<KeywordResponse>> GetKeywordsAsync(CancellationToken cancellationToken)
    {
        var items = await keywords.ListAsync(cancellationToken);
        return items.Select(ToKeywordResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<KeywordClusterResponse>> GetClustersAsync(CancellationToken cancellationToken)
    {
        var items = await keywords.ListAsync(cancellationToken);
        return items
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Cluster) ? "general" : x.Cluster)
            .Select(group => new KeywordClusterResponse(
                group.Key,
                group.Count(),
                Math.Round(group.Average(x => x.OpportunityScore), 2),
                group.OrderByDescending(x => x.OpportunityScore).Select(ToKeywordResponse).ToArray()))
            .OrderByDescending(x => x.AverageOpportunityScore)
            .ToArray();
    }

    public async Task<JobResponse> ResearchAsync(ResearchKeywordRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var context = new KeywordResearchContext(
            tenantId,
            request.SiteId,
            request.SeedKeyword,
            string.IsNullOrWhiteSpace(request.Country) ? options.Value.DefaultCountry : request.Country,
            string.IsNullOrWhiteSpace(request.Language) ? options.Value.DefaultLanguage : request.Language,
            request.Subreddits?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [],
            request.RssFeeds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? []);

        var result = await researchRunner.RunAsync(context, cancellationToken);
        var job = jobs.Enqueue(QueueNames.Keywords, "ResearchKeywordJob", tenantId, result.ResearchRunId);
        return job with { Status = $"Completed: {result.Keywords.Count} keywords" };
    }

    private Guid RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");

    private static KeywordResponse ToKeywordResponse(Keyword keyword) => new(
        keyword.Id,
        keyword.SiteId,
        keyword.Text,
        keyword.SearchVolume,
        keyword.Difficulty,
        keyword.TrendScore,
        keyword.OpportunityScore,
        keyword.Mentions,
        keyword.Source,
        keyword.Cluster,
        keyword.Intent,
        keyword.LastSeenAt);
}

public sealed class ArticleGenerationService(
    ICurrentUserContext currentUser,
    IArticleRepository articles,
    IUnitOfWork unitOfWork,
    IJobScheduler jobs) : IArticleGenerationService
{
    public async Task<IReadOnlyCollection<ArticleSummaryResponse>> GetArticlesAsync(CancellationToken cancellationToken)
    {
        var items = await articles.ListAsync(cancellationToken);
        return items.Select(ToSummary).ToArray();
    }

    public async Task<ArticleDetailResponse?> GetArticleAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await articles.GetAsync(articleId, cancellationToken);
        return article is null ? null : ToDetail(article);
    }

    public async Task<JobResponse> GenerateAsync(GenerateArticleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var article = new Article(Guid.NewGuid(), tenantId, request.SiteId, request.KeywordId, request.Title, "<p>Article generation job queued.</p>");
        article.MarkGenerating();
        await articles.AddAsync(article, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return jobs.Enqueue(QueueNames.Articles, "GenerateArticleJob", tenantId, article.Id);
    }

    private Guid RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");
    private static ArticleSummaryResponse ToSummary(Article article) => new(article.Id, article.SiteId, article.Title, article.Status.ToString(), article.SeoScore);
    private static ArticleDetailResponse ToDetail(Article article) => new(article.Id, article.SiteId, article.PrimaryKeywordId, article.Title, article.HtmlContent, article.Status.ToString(), article.SeoScore, article.WordPressPostId);
}

public sealed class SeoAnalysisService(
    ICurrentUserContext currentUser,
    IArticleRepository articles,
    IUnitOfWork unitOfWork,
    IJobScheduler jobs) : ISeoAnalysisService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "al", "algo", "and", "ante", "as", "at", "be", "by", "con", "como", "contra", "de", "del", "desde",
        "during", "e", "el", "ella", "ellas", "ellos", "en", "entre", "era", "erais", "eran", "eras", "eres",
        "es", "esa", "esas", "ese", "eso", "esos", "esta", "estaba", "estabais", "estaban", "estado", "estais",
        "estamos", "estan", "estar", "estas", "este", "esto", "estos", "for", "from", "fue", "fueron", "ha",
        "had", "has", "hasta", "hay", "he", "in", "into", "is", "la", "las", "le", "les", "lo", "los", "más",
        "mi", "mis", "muy", "no", "of", "on", "or", "para", "pero", "por", "que", "se", "ser", "si", "sin",
        "sobre", "son", "su", "sus", "the", "their", "them", "to", "tu", "tus", "un", "una", "uno", "unos",
        "y", "ya"
    };

    public Task<SeoAnalyzeResponse> AnalyzeAsync(SeoAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var analysis = BuildAnalysis(request.Title, request.HtmlContent, request.PrimaryKeyword);
        return Task.FromResult(analysis);
    }

    public async Task<JobResponse> AnalyzeArticleAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await articles.GetAsync(articleId, cancellationToken) ?? throw new InvalidOperationException("Article not found.");
        var analysis = BuildAnalysis(article.Title, article.HtmlContent, article.Title);
        article.AddSeoAnalysis(new ArticleSeoAnalysis(Guid.NewGuid(), article.TenantId, article.Id, analysis.Score, string.Join(" | ", analysis.Recommendations)));
        article.MarkReadyForReview(analysis.Score);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return jobs.Enqueue(QueueNames.Seo, "AnalyzeSeoJob", RequireTenant(), article.Id);
    }

    private Guid RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");

    private static SeoAnalyzeResponse BuildAnalysis(string title, string htmlContent, string primaryKeyword)
    {
        var normalizedKeyword = NormalizePhrase(primaryKeyword);
        var plainText = ExtractPlainText(htmlContent);
        var headings = ExtractHeadings(htmlContent);
        var paragraphs = htmlContent
            .Split(["</p>", "<br", "<br/>", "<br />"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ExtractPlainText)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        var words = ExtractWords(plainText);
        var sentenceCount = Math.Max(1, Regex.Matches(plainText, @"[.!?]+").Count);
        var paragraphCount = Math.Max(1, paragraphs.Length);
        var keywordHits = CountPhraseOccurrences(plainText, normalizedKeyword);
        var density = words.Count == 0 ? 0m : Math.Round(keywordHits / (decimal)words.Count * 100m, 2);
        var semanticKeywords = BuildSemanticKeywords(words, normalizedKeyword);
        var readabilityMetrics = BuildReadabilityMetrics(words, sentenceCount, paragraphCount);
        var faqs = BuildFaqs(title, normalizedKeyword, headings, semanticKeywords);
        var internalLinks = BuildInternalLinks(normalizedKeyword, headings, semanticKeywords);
        var faqSchema = SerializeJsonLd(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = faqs.Select(static faq => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = faq.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = faq.Answer
                }
            }).ToArray()
        });
        var articleJsonLd = SerializeJsonLd(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = string.IsNullOrWhiteSpace(title) ? normalizedKeyword : title.Trim(),
            ["description"] = BuildDescription(paragraphs, normalizedKeyword),
            ["keywords"] = string.Join(", ", semanticKeywords.Prepend(normalizedKeyword).Where(static x => !string.IsNullOrWhiteSpace(x))),
            ["about"] = normalizedKeyword,
            ["articleSection"] = headings.Length > 0 ? headings : semanticKeywords.ToArray()
        });
        var recommendations = BuildRecommendations(density, readabilityMetrics, semanticKeywords, internalLinks, headings);
        var score = CalculateScore(density, readabilityMetrics.ReadingEase, semanticKeywords.Count, internalLinks.Count, faqs.Count, headings.Length, keywordHits);
        return new SeoAnalyzeResponse(
            score,
            density,
            readabilityMetrics.GradeLevel,
            recommendations,
            semanticKeywords,
            internalLinks,
            faqs,
            faqSchema,
            articleJsonLd,
            readabilityMetrics);
    }

    private static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = Regex.Replace(html, "<(script|style)[^>]*>.*?</\\1>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var withoutTags = Regex.Replace(withoutScripts, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, "\\s+", " ").Trim();
    }

    private static string[] ExtractHeadings(string html) =>
        Regex.Matches(html ?? string.Empty, "<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(match => ExtractPlainText(match.Groups[1].Value))
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static List<string> ExtractWords(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .Where(static word => word.Length > 1)
            .ToList();

    private static int CountPhraseOccurrences(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase))
        {
            return 0;
        }

        return Regex.Matches(text, $@"\b{Regex.Escape(phrase)}\b", RegexOptions.IgnoreCase).Count;
    }

    private static IReadOnlyCollection<string> BuildSemanticKeywords(IReadOnlyCollection<string> words, string primaryKeyword)
    {
        var excluded = new HashSet<string>(
            ExtractWords(primaryKeyword).Concat([NormalizePhrase(primaryKeyword)]),
            StringComparer.OrdinalIgnoreCase);

        return words
            .Where(word => word.Length > 2 && !StopWords.Contains(word) && !excluded.Contains(word))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(group => group.Key)
            .ToArray();
    }

    private static SeoReadabilityMetrics BuildReadabilityMetrics(IReadOnlyCollection<string> words, int sentenceCount, int paragraphCount)
    {
        var wordCount = words.Count;
        var syllables = Math.Max(1, words.Sum(CountSyllables));
        var readingEase = wordCount == 0
            ? 0m
            : Math.Round((decimal)(206.835 - (1.015 * (wordCount / (double)sentenceCount)) - (84.6 * (syllables / (double)wordCount))), 2);

        return new SeoReadabilityMetrics(wordCount, sentenceCount, paragraphCount, readingEase, MapReadability(readingEase));
    }

    private static int CountSyllables(string word)
    {
        var normalized = Regex.Replace(word.ToLowerInvariant(), @"[^a-záéíóúü]", string.Empty);
        if (normalized.Length == 0)
        {
            return 1;
        }

        var matches = Regex.Matches(normalized, @"[aeiouáéíóúüy]+").Count;
        return Math.Max(1, matches);
    }

    private static string MapReadability(decimal readingEase) => readingEase switch
    {
        >= 80m => "Easy",
        >= 60m => "Good",
        >= 40m => "Moderate",
        _ => "Hard"
    };

    private static IReadOnlyCollection<SeoFaqItem> BuildFaqs(string title, string primaryKeyword, IReadOnlyList<string> headings, IReadOnlyCollection<string> semanticKeywords)
    {
        var subject = string.IsNullOrWhiteSpace(primaryKeyword) ? title.Trim() : primaryKeyword.Trim();
        var supportingTerms = semanticKeywords.Take(3).ToArray();
        var baseFaqs = new[]
        {
            new SeoFaqItem(
                $"What is {subject}?",
                $"{subject} is the main focus of this article, explained with practical context and search-friendly structure."),
            new SeoFaqItem(
                $"Why is {subject} important?",
                supportingTerms.Length == 0
                    ? $"{subject} matters because it helps readers understand the topic quickly and take action."
                    : $"{subject} matters because it connects with {string.Join(", ", supportingTerms)} and answers related search intent."),
            new SeoFaqItem(
                $"How can you improve {subject}?",
                "Improve it with clear headings, concise paragraphs, supporting entities, relevant internal links, and structured data.")
        };

        return headings
            .Where(static heading => heading.EndsWith("?", StringComparison.Ordinal))
            .Select(heading => new SeoFaqItem(heading, $"This section answers {heading.TrimEnd('?')} with direct, concise guidance."))
            .Concat(baseFaqs)
            .DistinctBy(static faq => faq.Question, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static IReadOnlyCollection<SeoInternalLinkSuggestion> BuildInternalLinks(string primaryKeyword, IReadOnlyList<string> headings, IReadOnlyCollection<string> semanticKeywords)
    {
        return headings
            .Concat(semanticKeywords.Select(term => $"{term} guide"))
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(text => !string.Equals(text, primaryKeyword, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(text => new SeoInternalLinkSuggestion(
                text,
                $"/{Slugify(text)}",
                $"Supports topical authority around {primaryKeyword}."))
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildRecommendations(
        decimal density,
        SeoReadabilityMetrics readability,
        IReadOnlyCollection<string> semanticKeywords,
        IReadOnlyCollection<SeoInternalLinkSuggestion> internalLinks,
        IReadOnlyCollection<string> headings)
    {
        var recommendations = new List<string>();

        if (density < 0.8m)
        {
            recommendations.Add("Increase primary keyword usage in body and headings.");
        }
        else if (density > 2.5m)
        {
            recommendations.Add("Reduce keyword repetition to avoid stuffing.");
        }

        if (semanticKeywords.Count < 5)
        {
            recommendations.Add("Expand semantic coverage with related entities and modifiers.");
        }

        if (readability.ReadingEase < 60m)
        {
            recommendations.Add("Shorten sentences and simplify wording for better readability.");
        }

        if (internalLinks.Count < 3)
        {
            recommendations.Add("Add more internal links to supporting pages or clusters.");
        }

        if (headings.Count < 2)
        {
            recommendations.Add("Use more semantic headings to improve topical structure.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("SEO baseline is strong. Keep metadata and links aligned with search intent.");
        }

        return recommendations;
    }

    private static int CalculateScore(decimal density, decimal readingEase, int semanticCount, int internalLinkCount, int faqCount, int headingCount, int keywordHits)
    {
        var score = 0;
        score += density is >= 0.8m and <= 2.5m ? 25 : density > 0 ? 14 : 0;
        score += Math.Clamp((int)Math.Round(readingEase / 4m), 0, 25);
        score += Math.Min(15, semanticCount * 3);
        score += Math.Min(15, internalLinkCount * 4);
        score += Math.Min(10, faqCount * 3);
        score += Math.Min(10, headingCount * 2);
        score += keywordHits > 0 ? 5 : 0;
        return Math.Clamp(score, 0, 100);
    }

    private static string BuildDescription(IReadOnlyList<string> paragraphs, string primaryKeyword)
    {
        var source = paragraphs.FirstOrDefault(static text => text.Length > 30) ?? $"Learn about {primaryKeyword}.";
        return source.Length <= 160 ? source : $"{source[..157].TrimEnd()}...";
    }

    private static string Slugify(string value)
    {
        var normalized = NormalizePhrase(value);
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", "-");
        return normalized.Trim('-').ToLowerInvariant();
    }

    private static string NormalizePhrase(string value) =>
        Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), "\\s+", " ");

    private static string SerializeJsonLd(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public sealed class ArticleWorkflowService(
    ICurrentUserContext currentUser,
    IArticleRepository articles,
    ISiteRepository sites,
    IWordPressPublishingService wordPressPublishing,
    IUnitOfWork unitOfWork) : IArticleWorkflowService
{
    public async Task<ArticleDetailResponse> ApproveAsync(Guid articleId, Guid approvedByUserId, CancellationToken cancellationToken)
    {
        var currentUserId = currentUser.UserId ?? throw new UnauthorizedAccessException("Authenticated user is required.");
        if (approvedByUserId != currentUserId)
        {
            throw new UnauthorizedAccessException("The approving user must match the authenticated user.");
        }

        var article = await articles.GetAsync(articleId, cancellationToken) ?? throw new InvalidOperationException("Article not found.");
        article.Approve(approvedByUserId, DateTimeOffset.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDetail(article);
    }

    public async Task<JobResponse> PublishAsync(Guid articleId, PublishArticleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");
        var article = await articles.GetAsync(articleId, cancellationToken) ?? throw new InvalidOperationException("Article not found.");
        if (article.SiteId != request.SiteId)
        {
            throw new InvalidOperationException("Article does not belong to the requested site.");
        }

        var site = await sites.GetAsync(request.SiteId, cancellationToken) ?? throw new InvalidOperationException("Site not found.");
        article.MarkPublishing();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var publishResult = await wordPressPublishing.PublishAsync(
                article,
                site,
                new WordPressPublishRequest(
                    request.Mode,
                    request.ScheduledFor,
                    request.Categories?.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
                    request.Tags?.Where(static x => !string.IsNullOrWhiteSpace(x)).Select(static x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
                    request.FeaturedImageUrl,
                    request.FeaturedImageAltText,
                    request.Slug,
                    request.Excerpt),
                cancellationToken);

            article.MarkPublished(publishResult.PostId, DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new JobResponse(
                publishResult.PostId,
                QueueNames.Publishing,
                $"Completed: WordPress post {publishResult.Status}");
        }
        catch
        {
            article.MarkPublishFailed();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static ArticleDetailResponse ToDetail(Article article) => new(article.Id, article.SiteId, article.PrimaryKeywordId, article.Title, article.HtmlContent, article.Status.ToString(), article.SeoScore, article.WordPressPostId);
}

public sealed class SiteService(
    ICurrentUserContext currentUser,
    ISiteRepository sites,
    IUnitOfWork unitOfWork,
    ISecretProtector secretProtector) : ISiteService
{
    public async Task<IReadOnlyCollection<SiteResponse>> GetSitesAsync(CancellationToken cancellationToken)
    {
        var items = await sites.ListAsync(cancellationToken);
        return items.Select(ToResponse).ToArray();
    }

    public async Task<SiteResponse> CreateAsync(CreateSiteRequest request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        var site = new Site(Guid.NewGuid(), tenantId, request.Name, new Uri(request.BaseUrl));
        await sites.AddAsync(site, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(site);
    }

    public async Task<SiteResponse> ConfigureWordPressAsync(Guid siteId, UpdateWordPressSettingsRequest request, CancellationToken cancellationToken)
    {
        var site = await sites.GetAsync(siteId, cancellationToken) ?? throw new InvalidOperationException("Site not found.");
        site.ConfigureWordPress(request.Username, secretProtector.Protect(request.ApplicationPassword));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(site);
    }

    private static SiteResponse ToResponse(Site site) => new(site.Id, site.TenantId, site.Name, site.BaseUrl.ToString(), site.WordPressConnection is not null);
}
