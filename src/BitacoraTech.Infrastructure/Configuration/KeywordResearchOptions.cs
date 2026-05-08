namespace BitacoraTech.Infrastructure.Configuration;

public sealed class KeywordResearchOptions
{
    public const string SectionName = "KeywordResearch";

    public string DefaultCountry { get; set; } = "US";
    public string DefaultLanguage { get; set; } = "en-US";
    public int SchedulerIntervalMinutes { get; set; } = 360;
    public int MaxCandidatesPerSource { get; set; } = 25;
    public List<string> RedditSubreddits { get; set; } = ["technology", "SEO", "smallbusiness"];
    public List<string> RssFeeds { get; set; } = [];
}
