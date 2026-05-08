using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Articles;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace BitacoraTech.Infrastructure.WordPress;

public sealed class WordPressRestPublishingService(
    IHttpClientFactory httpClientFactory,
    ISecretProtector secretProtector,
    ILogger<WordPressRestPublishingService> logger) : IWordPressPublishingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<WordPressPublishResult> PublishAsync(
        Article article,
        Site site,
        WordPressPublishRequest request,
        CancellationToken cancellationToken)
    {
        if (!article.CanPublish)
        {
            throw new InvalidOperationException("WordPress publishing requires explicit human approval.");
        }

        var connection = site.WordPressConnection ?? throw new InvalidOperationException("WordPress connection is not configured for this site.");
        ValidateRequest(request);

        using var wordPressClient = CreateAuthenticatedClient(site, connection);

        var categoryIds = await EnsureTermsAsync(wordPressClient, "categories", request.Categories, cancellationToken);
        var tagIds = await EnsureTermsAsync(wordPressClient, "tags", request.Tags, cancellationToken);
        var featuredMediaId = await UploadFeaturedMediaAsync(
            request.FeaturedImageUrl,
            request.FeaturedImageAltText ?? article.Title,
            wordPressClient,
            cancellationToken);

        var payload = BuildPostPayload(article, request, categoryIds, tagIds, featuredMediaId);
        using var response = await wordPressClient.PostAsJsonAsync("wp-json/wp/v2/posts", payload, JsonOptions, cancellationToken);
        var post = await ReadRequiredAsync<WordPressPostResponse>(response, cancellationToken);

        logger.LogInformation(
            "Published article {ArticleId} to WordPress site {SiteId} as post {PostId} with status {Status}",
            article.Id,
            site.Id,
            post.Id,
            post.Status);

        return new WordPressPublishResult(post.Id.ToString(), post.Status, post.Link, featuredMediaId);
    }

    private static Dictionary<string, object?> BuildPostPayload(
        Article article,
        WordPressPublishRequest request,
        IReadOnlyCollection<int> categoryIds,
        IReadOnlyCollection<int> tagIds,
        int? featuredMediaId)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = article.Title,
            ["content"] = article.HtmlContent,
            ["status"] = ToWordPressStatus(request.Mode)
        };

        AddIfPresent(payload, "slug", NormalizeOptional(request.Slug));
        AddIfPresent(payload, "excerpt", NormalizeOptional(request.Excerpt));
        AddIfPresent(payload, "categories", categoryIds.Count == 0 ? null : categoryIds);
        AddIfPresent(payload, "tags", tagIds.Count == 0 ? null : tagIds);
        AddIfPresent(payload, "featured_media", featuredMediaId);

        if (request.Mode == WordPressPublicationMode.Schedule && request.ScheduledFor is not null)
        {
            payload["date_gmt"] = request.ScheduledFor.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss");
        }

        return payload;
    }

    private async Task<IReadOnlyCollection<int>> EnsureTermsAsync(
        HttpClient client,
        string taxonomy,
        IReadOnlyCollection<string> terms,
        CancellationToken cancellationToken)
    {
        if (terms.Count == 0)
        {
            return [];
        }

        var ids = new List<int>(terms.Count);
        foreach (var term in terms)
        {
            ids.Add(await EnsureTermAsync(client, taxonomy, term, cancellationToken));
        }

        return ids;
    }

    private async Task<int> EnsureTermAsync(HttpClient client, string taxonomy, string termName, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(termName);
        using var lookupResponse = await client.GetAsync($"wp-json/wp/v2/{taxonomy}?search={encoded}&per_page=100", cancellationToken);
        var matches = await ReadRequiredAsync<List<WordPressTermResponse>>(lookupResponse, cancellationToken);

        var existing = matches.FirstOrDefault(x => string.Equals(x.Name, termName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing.Id;
        }

        using var createResponse = await client.PostAsJsonAsync($"wp-json/wp/v2/{taxonomy}", new { name = termName }, JsonOptions, cancellationToken);
        if (createResponse.IsSuccessStatusCode)
        {
            var created = await ReadRequiredAsync<WordPressTermResponse>(createResponse, cancellationToken);
            return created.Id;
        }

        if (createResponse.StatusCode != HttpStatusCode.BadRequest)
        {
            await ThrowWordPressErrorAsync(createResponse, cancellationToken);
        }

        using var fallbackLookup = await client.GetAsync($"wp-json/wp/v2/{taxonomy}?search={encoded}&per_page=100", cancellationToken);
        var fallbackMatches = await ReadRequiredAsync<List<WordPressTermResponse>>(fallbackLookup, cancellationToken);
        var fallback = fallbackMatches.FirstOrDefault(x => string.Equals(x.Name, termName, StringComparison.OrdinalIgnoreCase));
        if (fallback is not null)
        {
            return fallback.Id;
        }

        await ThrowWordPressErrorAsync(createResponse, cancellationToken);
        return 0;
    }

    private async Task<int?> UploadFeaturedMediaAsync(
        string? featuredImageUrl,
        string altText,
        HttpClient wordPressClient,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(featuredImageUrl))
        {
            return null;
        }

        using var mediaSourceClient = httpClientFactory.CreateClient();
        using var mediaResponse = await mediaSourceClient.GetAsync(featuredImageUrl, cancellationToken);
        mediaResponse.EnsureSuccessStatusCode();

        var bytes = await mediaResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        using var mediaContent = new ByteArrayContent(bytes);
        mediaContent.Headers.ContentType = mediaResponse.Content.Headers.ContentType ?? new MediaTypeHeaderValue("application/octet-stream");

        var fileName = GetFileName(featuredImageUrl, mediaResponse.Content.Headers.ContentDisposition?.FileNameStar, mediaResponse.Content.Headers.ContentDisposition?.FileName);
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "wp-json/wp/v2/media")
        {
            Content = mediaContent
        };
        uploadRequest.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = fileName
        };

        using var uploadResponse = await wordPressClient.SendAsync(uploadRequest, cancellationToken);
        var uploaded = await ReadRequiredAsync<WordPressMediaResponse>(uploadResponse, cancellationToken);

        if (!string.IsNullOrWhiteSpace(altText))
        {
            using var altUpdateResponse = await wordPressClient.PostAsJsonAsync(
                $"wp-json/wp/v2/media/{uploaded.Id}",
                new Dictionary<string, object?> { ["alt_text"] = altText.Trim() },
                JsonOptions,
                cancellationToken);

            await ReadRequiredAsync<WordPressMediaResponse>(altUpdateResponse, cancellationToken);
        }

        return uploaded.Id;
    }

    private HttpClient CreateAuthenticatedClient(Site site, WordPressConnection connection)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = site.BaseUrl;

        var applicationPassword = secretProtector.Unprotect(connection.EncryptedApplicationPassword);
        var rawCredentials = $"{connection.Username}:{applicationPassword}";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials)));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static void ValidateRequest(WordPressPublishRequest request)
    {
        if (request.Mode == WordPressPublicationMode.Schedule && request.ScheduledFor is null)
        {
            throw new InvalidOperationException("Scheduling a WordPress post requires a future date.");
        }

        if (request.Mode == WordPressPublicationMode.Schedule && request.ScheduledFor <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Scheduled WordPress posts must use a future date.");
        }
    }

    private static string ToWordPressStatus(WordPressPublicationMode mode) => mode switch
    {
        WordPressPublicationMode.Draft => "draft",
        WordPressPublicationMode.Publish => "publish",
        WordPressPublicationMode.Schedule => "future",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported WordPress publication mode.")
    };

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowWordPressErrorAsync(response, cancellationToken);
        }

        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("WordPress returned an empty response.");
    }

    private static async Task ThrowWordPressErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"WordPress API error {(int)response.StatusCode}: {body}");
    }

    private static string GetFileName(string url, string? fileNameStar, string? fileName)
    {
        var candidate = NormalizeFileName(fileNameStar) ?? NormalizeFileName(fileName);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var uri = new Uri(url);
        var lastSegment = uri.Segments.LastOrDefault();
        return NormalizeFileName(lastSegment) ?? $"article-image-{Guid.NewGuid():N}.bin";
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return fileName.Trim().Trim('"');
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddIfPresent(IDictionary<string, object?> payload, string key, object? value)
    {
        if (value is not null)
        {
            payload[key] = value;
        }
    }

    private sealed record WordPressTermResponse(int Id, string Name);
    private sealed record WordPressMediaResponse(int Id);
    private sealed record WordPressPostResponse(int Id, string Status, string? Link);
}
