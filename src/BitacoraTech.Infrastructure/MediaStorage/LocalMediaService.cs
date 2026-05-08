using BitacoraTech.Application.Common;

namespace BitacoraTech.Infrastructure.MediaStorage;

public sealed class LocalMediaService : IMediaService
{
    public Task<string> UploadArticleImageAsync(Guid articleId, Stream content, string fileName, CancellationToken cancellationToken)
    {
        return Task.FromResult($"/media/articles/{articleId:N}/{Uri.EscapeDataString(fileName)}");
    }
}

