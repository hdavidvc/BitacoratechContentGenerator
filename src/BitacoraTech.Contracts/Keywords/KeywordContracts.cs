namespace BitacoraTech.Contracts.Keywords;

public sealed record KeywordResponse(
    Guid Id,
    Guid SiteId,
    string Keyword,
    int SearchVolume,
    decimal Difficulty,
    decimal TrendScore,
    decimal OpportunityScore,
    int Mentions,
    string Source,
    string Cluster,
    string Intent,
    DateTimeOffset LastSeenAt);

public sealed record ResearchKeywordRequest(
    Guid SiteId,
    string SeedKeyword,
    string Country = "US",
    string Language = "en-US",
    IReadOnlyCollection<string>? Subreddits = null,
    IReadOnlyCollection<string>? RssFeeds = null);

public sealed record KeywordClusterResponse(string Name, int KeywordCount, decimal AverageOpportunityScore, IReadOnlyCollection<KeywordResponse> Keywords);
