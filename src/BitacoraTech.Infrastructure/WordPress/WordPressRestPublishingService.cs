using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Sites;

namespace BitacoraTech.Infrastructure.WordPress;

public sealed class WordPressRestPublishingService : IWordPressPublishingService
{
    public Task<string> PublishAsync(Article article, Site site, CancellationToken cancellationToken)
    {
        if (!article.CanPublish)
        {
            throw new InvalidOperationException("WordPress publishing requires explicit human approval.");
        }

        return Task.FromResult($"wp-{article.Id:N}");
    }
}

