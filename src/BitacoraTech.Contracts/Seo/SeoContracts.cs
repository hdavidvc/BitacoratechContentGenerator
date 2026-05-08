namespace BitacoraTech.Contracts.Seo;

public sealed record SeoAnalyzeRequest(Guid? ArticleId, string Title, string HtmlContent, string PrimaryKeyword);
public sealed record SeoAnalyzeResponse(
    int Score,
    decimal KeywordDensity,
    string Readability,
    IReadOnlyCollection<string> Recommendations,
    IReadOnlyCollection<string> SemanticKeywords,
    IReadOnlyCollection<SeoInternalLinkSuggestion> InternalLinks,
    IReadOnlyCollection<SeoFaqItem> Faqs,
    string FaqSchema,
    string JsonLd,
    SeoReadabilityMetrics ReadabilityMetrics);

public sealed record SeoInternalLinkSuggestion(string AnchorText, string Slug, string Reason);
public sealed record SeoFaqItem(string Question, string Answer);
public sealed record SeoReadabilityMetrics(int WordCount, int SentenceCount, int ParagraphCount, decimal ReadingEase, string GradeLevel);
