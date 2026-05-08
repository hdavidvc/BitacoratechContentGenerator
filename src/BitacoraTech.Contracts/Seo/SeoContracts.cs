namespace BitacoraTech.Contracts.Seo;

public sealed record SeoAnalyzeRequest(Guid? ArticleId, string Title, string HtmlContent, string PrimaryKeyword);
public sealed record SeoAnalyzeResponse(int Score, decimal KeywordDensity, string Readability, IReadOnlyCollection<string> Recommendations);

