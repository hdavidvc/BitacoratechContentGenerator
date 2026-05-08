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
using Microsoft.Extensions.Options;

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
    public Task<SeoAnalyzeResponse> AnalyzeAsync(SeoAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var words = request.HtmlContent.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keywordHits = words.Count(word => string.Equals(word.Trim(' ', '.', ',', ';', ':', '!', '?'), request.PrimaryKeyword, StringComparison.OrdinalIgnoreCase));
        var density = words.Length == 0 ? 0 : Math.Round((decimal)keywordHits / words.Length * 100, 2);
        var score = Math.Clamp(70 + Math.Min(20, keywordHits * 2), 0, 100);
        IReadOnlyCollection<string> recommendations = ["Add FAQ JSON-LD", "Improve internal linking", "Include semantic keyword variants"];
        return Task.FromResult(new SeoAnalyzeResponse(score, density, "Good", recommendations));
    }

    public async Task<JobResponse> AnalyzeArticleAsync(Guid articleId, CancellationToken cancellationToken)
    {
        var article = await articles.GetAsync(articleId, cancellationToken) ?? throw new InvalidOperationException("Article not found.");
        article.AddSeoAnalysis(new ArticleSeoAnalysis(Guid.NewGuid(), article.TenantId, article.Id, 82, "Semantic SEO analysis queued and persisted."));
        article.MarkReadyForReview(82);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return jobs.Enqueue(QueueNames.Seo, "AnalyzeSeoJob", RequireTenant(), article.Id);
    }

    private Guid RequireTenant() => currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");
}

public sealed class ArticleWorkflowService(
    ICurrentUserContext currentUser,
    IArticleRepository articles,
    IUnitOfWork unitOfWork,
    IJobScheduler jobs) : IArticleWorkflowService
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

    public async Task<JobResponse> PublishAsync(Guid articleId, Guid siteId, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException("Authenticated tenant is required.");
        var article = await articles.GetAsync(articleId, cancellationToken) ?? throw new InvalidOperationException("Article not found.");
        if (article.SiteId != siteId)
        {
            throw new InvalidOperationException("Article does not belong to the requested site.");
        }

        article.MarkPublishing();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return jobs.Enqueue(QueueNames.Publishing, "PublishToWordPressJob", tenantId, article.Id);
    }

    private static ArticleDetailResponse ToDetail(Article article) => new(article.Id, article.SiteId, article.PrimaryKeywordId, article.Title, article.HtmlContent, article.Status.ToString(), article.SeoScore, article.WordPressPostId);
}

public sealed class SiteService(
    ICurrentUserContext currentUser,
    ISiteRepository sites,
    IUnitOfWork unitOfWork) : ISiteService
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
        site.ConfigureWordPress(request.Username, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.ApplicationPassword)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(site);
    }

    private static SiteResponse ToResponse(Site site) => new(site.Id, site.TenantId, site.Name, site.BaseUrl.ToString(), site.WordPressConnection is not null);
}
