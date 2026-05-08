using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Articles;

public sealed class ArticleSeoAnalysis : Entity
{
    private ArticleSeoAnalysis()
    {
        Summary = string.Empty;
    }

    public ArticleSeoAnalysis(Guid id, Guid tenantId, Guid articleId, int score, string summary)
        : base(id)
    {
        TenantId = tenantId;
        ArticleId = articleId;
        Score = Math.Clamp(score, 0, 100);
        Summary = summary;
    }

    public Guid TenantId { get; private set; }
    public Guid ArticleId { get; private set; }
    public int Score { get; private set; }
    public string Summary { get; private set; }
}

public sealed class ArticleImage : Entity
{
    private ArticleImage()
    {
        Url = string.Empty;
        AltText = string.Empty;
    }

    public ArticleImage(Guid id, Guid tenantId, Guid articleId, string url, string altText)
        : base(id)
    {
        TenantId = tenantId;
        ArticleId = articleId;
        Url = url;
        AltText = altText;
    }

    public Guid TenantId { get; private set; }
    public Guid ArticleId { get; private set; }
    public string Url { get; private set; }
    public string AltText { get; private set; }
}

public sealed class ArticleApproval : Entity
{
    private ArticleApproval()
    {
    }

    public ArticleApproval(Guid id, Guid tenantId, Guid articleId, Guid approvedByUserId, DateTimeOffset approvedAt)
        : base(id)
    {
        TenantId = tenantId;
        ArticleId = articleId;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = approvedAt;
    }

    public Guid TenantId { get; private set; }
    public Guid ArticleId { get; private set; }
    public Guid ApprovedByUserId { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
}

public sealed class Category : Entity
{
    private Category()
    {
        Name = string.Empty;
    }

    public Category(Guid id, Guid tenantId, Guid siteId, string name, string? wordpressId = null)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        Name = name;
        WordPressId = wordpressId;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public string Name { get; private set; }
    public string? WordPressId { get; private set; }
}

public sealed class Tag : Entity
{
    private Tag()
    {
        Name = string.Empty;
    }

    public Tag(Guid id, Guid tenantId, Guid siteId, string name, string? wordpressId = null)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        Name = name;
        WordPressId = wordpressId;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public string Name { get; private set; }
    public string? WordPressId { get; private set; }
}

public enum PublishingStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}

public sealed class PublishingJob : Entity
{
    private PublishingJob()
    {
    }

    public PublishingJob(Guid id, Guid tenantId, Guid siteId, Guid articleId, PublishingStatus status)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        ArticleId = articleId;
        Status = status;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid ArticleId { get; private set; }
    public PublishingStatus Status { get; private set; }
}
