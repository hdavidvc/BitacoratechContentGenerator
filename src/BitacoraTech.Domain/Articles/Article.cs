using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Articles;

public sealed class Article : Entity
{
    private readonly List<ArticleSeoAnalysis> _seoAnalyses = [];
    private readonly List<ArticleImage> _images = [];
    private readonly List<PublishingJob> _publishingJobs = [];
    private readonly List<Category> _categories = [];
    private readonly List<Tag> _tags = [];

    private Article()
    {
        Title = string.Empty;
        HtmlContent = string.Empty;
    }

    public Article(Guid id, Guid tenantId, Guid siteId, Guid? primaryKeywordId, string title, string htmlContent)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        PrimaryKeywordId = primaryKeywordId;
        Title = RequireText(title, nameof(title));
        HtmlContent = htmlContent ?? string.Empty;
        Status = ArticleStatus.Draft;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid? PrimaryKeywordId { get; private set; }
    public string Title { get; private set; }
    public string HtmlContent { get; private set; }
    public int? SeoScore { get; private set; }
    public ArticleStatus Status { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string? WordPressPostId { get; private set; }
    public ArticleApproval? Approval { get; private set; }
    public IReadOnlyCollection<ArticleSeoAnalysis> SeoAnalyses => _seoAnalyses;
    public IReadOnlyCollection<ArticleImage> Images => _images;
    public IReadOnlyCollection<PublishingJob> PublishingJobs => _publishingJobs;
    public IReadOnlyCollection<Category> Categories => _categories;
    public IReadOnlyCollection<Tag> Tags => _tags;

    public bool CanPublish => Status == ArticleStatus.Approved && ApprovedAt is not null && ApprovedByUserId is not null;

    public void MarkGenerating()
    {
        EnsureStatus(ArticleStatus.Draft, ArticleStatus.GenerationFailed);
        Status = ArticleStatus.Generating;
        Touch();
    }

    public void MarkGenerationFailed()
    {
        EnsureStatus(ArticleStatus.Generating);
        Status = ArticleStatus.GenerationFailed;
        Touch();
    }

    public void CompleteGeneration(string title, string htmlContent)
    {
        EnsureStatus(ArticleStatus.Generating, ArticleStatus.Draft);
        Title = RequireText(title, nameof(title));
        HtmlContent = RequireText(htmlContent, nameof(htmlContent));
        Status = ArticleStatus.Generated;
        Touch();
    }

    public void MarkSeoAnalyzing()
    {
        EnsureStatus(ArticleStatus.Generated, ArticleStatus.SeoFailed);
        Status = ArticleStatus.SeoAnalyzing;
        Touch();
    }

    public void MarkReadyForReview(int seoScore)
    {
        SeoScore = seoScore;
        Status = ArticleStatus.ReadyForReview;
        Touch();
    }

    public void MarkSeoFailed()
    {
        EnsureStatus(ArticleStatus.SeoAnalyzing, ArticleStatus.Generated);
        Status = ArticleStatus.SeoFailed;
        Touch();
    }

    public void Approve(Guid approvedByUserId, DateTimeOffset approvedAt)
    {
        EnsureStatus(ArticleStatus.ReadyForReview, ArticleStatus.Generated);
        if (approvedByUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Approval requires a valid user.");
        }

        ApprovedByUserId = approvedByUserId;
        ApprovedAt = approvedAt;
        Approval = new ArticleApproval(Guid.NewGuid(), TenantId, Id, approvedByUserId, approvedAt);
        Status = ArticleStatus.Approved;
        Touch();
    }

    public void MarkPublishing()
    {
        if (!CanPublish)
        {
            throw new InvalidOperationException("Publishing requires explicit human approval.");
        }

        Status = ArticleStatus.Publishing;
        _publishingJobs.Add(new PublishingJob(Guid.NewGuid(), TenantId, SiteId, Id, PublishingStatus.Queued));
        Touch();
    }

    public void MarkPublished(string wordpressPostId, DateTimeOffset publishedAt)
    {
        EnsureStatus(ArticleStatus.Publishing);
        WordPressPostId = RequireText(wordpressPostId, nameof(wordpressPostId));
        PublishedAt = publishedAt;
        Status = ArticleStatus.Published;
        Touch();
    }

    public void MarkPublishFailed()
    {
        EnsureStatus(ArticleStatus.Publishing, ArticleStatus.Approved);
        Status = ArticleStatus.PublishFailed;
        Touch();
    }

    public void AddSeoAnalysis(ArticleSeoAnalysis analysis)
    {
        if (analysis.ArticleId != Id)
        {
            throw new InvalidOperationException("SEO analysis belongs to another article.");
        }

        _seoAnalyses.Add(analysis);
        SeoScore = analysis.Score;
        Touch();
    }

    public void AddImage(ArticleImage image)
    {
        if (image.ArticleId != Id)
        {
            throw new InvalidOperationException("Article image belongs to another article.");
        }

        if (_images.Any(existing => string.Equals(existing.Url, image.Url, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _images.Add(image);
        Touch();
    }

    private void EnsureStatus(params ArticleStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException($"Article status {Status} is not valid for this transition.");
        }
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
