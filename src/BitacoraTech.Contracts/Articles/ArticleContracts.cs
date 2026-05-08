namespace BitacoraTech.Contracts.Articles;

public sealed record ArticleSummaryResponse(Guid Id, Guid SiteId, string Title, string Status, int? SeoScore);
public sealed record ArticleDetailResponse(Guid Id, Guid SiteId, Guid? PrimaryKeywordId, string Title, string HtmlContent, string Status, int? SeoScore, string? WordPressPostId);
public sealed record GenerateArticleRequest(Guid SiteId, Guid? KeywordId, string Title);
public sealed record ApproveArticleRequest(Guid ApprovedByUserId);
public sealed record PublishArticleRequest(Guid SiteId);

