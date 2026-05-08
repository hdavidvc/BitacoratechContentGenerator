namespace BitacoraTech.Domain.Articles;

public enum ArticleStatus
{
    Draft,
    Generating,
    Generated,
    SeoAnalyzing,
    ReadyForReview,
    Approved,
    Publishing,
    Published,
    GenerationFailed,
    SeoFailed,
    PublishFailed,
    Archived
}

