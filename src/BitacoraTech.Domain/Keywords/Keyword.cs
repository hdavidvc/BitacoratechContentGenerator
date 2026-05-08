using BitacoraTech.Domain.Common;

namespace BitacoraTech.Domain.Keywords;

public sealed class KeywordResearchRun : Entity
{
    private readonly List<Keyword> _keywords = [];

    private KeywordResearchRun()
    {
        Seed = string.Empty;
    }

    public KeywordResearchRun(Guid id, Guid tenantId, Guid siteId, string seed)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        Seed = seed.Trim();
        Status = KeywordResearchRunStatus.Pending;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public string Seed { get; private set; }
    public KeywordResearchRunStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyCollection<Keyword> Keywords => _keywords;

    public void AddKeyword(Keyword keyword) => _keywords.Add(keyword);

    public void MarkCompleted(DateTimeOffset completedAt)
    {
        Status = KeywordResearchRunStatus.Completed;
        CompletedAt = completedAt;
    }

    public void MarkFailed()
    {
        Status = KeywordResearchRunStatus.Failed;
    }
}

public sealed class Keyword : Entity
{
    private Keyword()
    {
        Text = string.Empty;
        Source = string.Empty;
        Cluster = string.Empty;
        Intent = string.Empty;
    }

    public Keyword(
        Guid id,
        Guid tenantId,
        Guid siteId,
        Guid? researchRunId,
        string text,
        int searchVolume,
        decimal difficulty,
        decimal trendScore = 0,
        decimal opportunityScore = 0,
        int mentions = 0,
        string source = "manual",
        string cluster = "",
        string intent = "informational",
        DateTimeOffset? lastSeenAt = null)
        : base(id)
    {
        TenantId = tenantId;
        SiteId = siteId;
        ResearchRunId = researchRunId;
        Text = text.Trim();
        SearchVolume = Math.Max(0, searchVolume);
        Difficulty = Math.Clamp(difficulty, 0, 100);
        TrendScore = Math.Clamp(trendScore, 0, 100);
        OpportunityScore = Math.Clamp(opportunityScore, 0, 100);
        Mentions = Math.Max(0, mentions);
        Source = source.Trim();
        Cluster = cluster.Trim();
        Intent = intent.Trim();
        LastSeenAt = lastSeenAt ?? DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Guid SiteId { get; private set; }
    public Guid? ResearchRunId { get; private set; }
    public string Text { get; private set; }
    public int SearchVolume { get; private set; }
    public decimal Difficulty { get; private set; }
    public decimal TrendScore { get; private set; }
    public decimal OpportunityScore { get; private set; }
    public int Mentions { get; private set; }
    public string Source { get; private set; }
    public string Cluster { get; private set; }
    public string Intent { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public void RefreshFromResearch(
        Guid? researchRunId,
        int searchVolume,
        decimal difficulty,
        decimal trendScore,
        decimal opportunityScore,
        int mentions,
        string source,
        string cluster,
        string intent,
        DateTimeOffset lastSeenAt)
    {
        ResearchRunId = researchRunId;
        SearchVolume = Math.Max(SearchVolume, searchVolume);
        Difficulty = Math.Clamp(difficulty, 0, 100);
        TrendScore = Math.Clamp(trendScore, 0, 100);
        OpportunityScore = Math.Clamp(opportunityScore, 0, 100);
        Mentions = Math.Max(Mentions, mentions);
        Source = source.Trim();
        Cluster = cluster.Trim();
        Intent = intent.Trim();
        LastSeenAt = lastSeenAt;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum KeywordResearchRunStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
