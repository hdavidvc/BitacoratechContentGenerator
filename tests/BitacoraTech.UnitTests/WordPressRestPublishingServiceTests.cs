using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Articles;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Security;
using BitacoraTech.Infrastructure.WordPress;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitacoraTech.UnitTests;

public sealed class WordPressRestPublishingServiceTests
{
    private static readonly ISecretProtector SecretProtector = new AesSecretProtector(
        Options.Create(new WordPressSecurityOptions { EncryptionKey = "unit-test-shared-secret-key-1234567890" }));

    [Fact]
    public async Task PublishAsync_CreatesTaxonomiesUploadsMediaAndSchedulesPost()
    {
        var scheduledFor = DateTimeOffset.UtcNow.AddHours(3);
        var article = CreateApprovedArticle();
        var site = CreateConfiguredSite();
        var capturedRequests = new List<HttpRequestMessage>();

        var httpClientFactory = new StubHttpClientFactory(request =>
        {
            capturedRequests.Add(CloneRequest(request));

            if (request.RequestUri?.Host == "cdn.example.com")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("image/png")
                        }
                    }
                });
            }

            var pathAndQuery = request.RequestUri?.PathAndQuery;
            return Task.FromResult(pathAndQuery switch
            {
                "/wp-json/wp/v2/categories?search=Tech&per_page=100" => Json(HttpStatusCode.OK, "[]"),
                "/wp-json/wp/v2/categories" => Json(HttpStatusCode.Created, """{"id":11,"name":"Tech"}"""),
                "/wp-json/wp/v2/tags?search=Automation&per_page=100" => Json(HttpStatusCode.OK, "[]"),
                "/wp-json/wp/v2/tags" => Json(HttpStatusCode.Created, """{"id":22,"name":"Automation"}"""),
                "/wp-json/wp/v2/media" => Json(HttpStatusCode.Created, """{"id":33}"""),
                "/wp-json/wp/v2/media/33" => Json(HttpStatusCode.OK, """{"id":33}"""),
                "/wp-json/wp/v2/posts" => Json(HttpStatusCode.Created, """{"id":44,"status":"future","link":"https://example.com/?p=44"}"""),
                _ => throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}")
            });
        });

        var service = new WordPressRestPublishingService(httpClientFactory, SecretProtector, NullLogger<WordPressRestPublishingService>.Instance);

        var result = await service.PublishAsync(
            article,
            site,
            new WordPressPublishRequest(
                WordPressPublicationMode.Schedule,
                scheduledFor,
                ["Tech"],
                ["Automation"],
                "https://cdn.example.com/uploads/featured.png",
                "Automation cover",
                "automation-at-scale",
                "WordPress integration excerpt"),
            CancellationToken.None);

        Assert.Equal("44", result.PostId);
        Assert.Equal("future", result.Status);
        Assert.Equal(33, result.FeaturedMediaId);

        var postRequest = capturedRequests.Single(x => x.RequestUri?.AbsolutePath == "/wp-json/wp/v2/posts");
        var payload = JsonDocument.Parse(await postRequest.Content!.ReadAsStringAsync());
        Assert.Equal("future", payload.RootElement.GetProperty("status").GetString());
        Assert.Equal("automation-at-scale", payload.RootElement.GetProperty("slug").GetString());
        Assert.Equal("WordPress integration excerpt", payload.RootElement.GetProperty("excerpt").GetString());
        Assert.Equal(
            scheduledFor.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss"),
            payload.RootElement.GetProperty("date_gmt").GetString());
        Assert.Equal(11, payload.RootElement.GetProperty("categories")[0].GetInt32());
        Assert.Equal(22, payload.RootElement.GetProperty("tags")[0].GetInt32());
        Assert.Equal(33, payload.RootElement.GetProperty("featured_media").GetInt32());

        var wpRequests = capturedRequests.Where(x => x.RequestUri?.Host == "example.com").ToArray();
        Assert.All(wpRequests, request =>
        {
            Assert.NotNull(request.Headers.Authorization);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        });
    }

    [Fact]
    public async Task PublishAsync_DraftModeCreatesDraftPostWithoutSchedule()
    {
        var article = CreateApprovedArticle();
        var site = CreateConfiguredSite();
        HttpRequestMessage? capturedPostRequest = null;

        var httpClientFactory = new StubHttpClientFactory(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/wp-json/wp/v2/posts")
            {
                capturedPostRequest = CloneRequest(request);
                return Task.FromResult(Json(HttpStatusCode.Created, """{"id":45,"status":"draft","link":"https://example.com/?p=45"}"""));
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var service = new WordPressRestPublishingService(httpClientFactory, SecretProtector, NullLogger<WordPressRestPublishingService>.Instance);

        var result = await service.PublishAsync(
            article,
            site,
            new WordPressPublishRequest(
                WordPressPublicationMode.Draft,
                null,
                [],
                [],
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.Equal("45", result.PostId);
        Assert.Equal("draft", result.Status);
        Assert.NotNull(capturedPostRequest);

        var payload = JsonDocument.Parse(await capturedPostRequest!.Content!.ReadAsStringAsync());
        Assert.Equal("draft", payload.RootElement.GetProperty("status").GetString());
        Assert.False(payload.RootElement.TryGetProperty("date_gmt", out _));
        Assert.False(payload.RootElement.TryGetProperty("featured_media", out _));
    }

    [Fact]
    public async Task PublishAsync_SupportsLegacyBase64Passwords()
    {
        var article = CreateApprovedArticle();
        var site = CreateConfiguredSite(useLegacyBase64: true);
        AuthenticationHeaderValue? authorization = null;

        var httpClientFactory = new StubHttpClientFactory(request =>
        {
            authorization = request.Headers.Authorization;
            return Task.FromResult(Json(HttpStatusCode.Created, """{"id":45,"status":"draft","link":"https://example.com/?p=45"}"""));
        });

        var service = new WordPressRestPublishingService(httpClientFactory, SecretProtector, NullLogger<WordPressRestPublishingService>.Instance);

        await service.PublishAsync(
            article,
            site,
            new WordPressPublishRequest(
                WordPressPublicationMode.Draft,
                null,
                [],
                [],
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.NotNull(authorization);
        Assert.Equal("Basic", authorization!.Scheme);
    }

    private static Article CreateApprovedArticle()
    {
        var article = new Article(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Automation at Scale", "<p>Content</p>");
        article.MarkGenerating();
        article.CompleteGeneration("Automation at Scale", "<p>Content</p>");
        article.MarkReadyForReview(90);
        article.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10));
        return article;
    }

    private static Site CreateConfiguredSite(bool useLegacyBase64 = false)
    {
        var site = new Site(Guid.NewGuid(), Guid.NewGuid(), "Main Site", new Uri("https://example.com/"));
        var password = useLegacyBase64
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes("app-password"))
            : SecretProtector.Protect("app-password");
        site.ConfigureWordPress("wp-user", password);
        return site;
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            clone.Content = new StringContent(body, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.Remove(header.Key);
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "") => new(new StubHttpMessageHandler(handler), disposeHandler: true);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }
}
