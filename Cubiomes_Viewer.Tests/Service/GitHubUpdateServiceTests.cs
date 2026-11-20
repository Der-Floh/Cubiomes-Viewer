using System.Net;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v5.2.0", "5.1.0", "5.2.0")]
    [InlineData("v10.0.0", "9.9.9", "10.0.0")]
    [InlineData("v5.1.0", "5.1.0-beta.2", "5.1.0")]
    [InlineData("v5.1.0", "5.1.0", null)]
    [InlineData("v5.0.0", "5.1.0", null)]
    public async Task FindNewerRelease_NewestReleaseOnGitHub_IsOnlyFoundWhenItIsNewer(string tag, string running, string? expected)
    {
        using var service = Create(new StubHandler(HttpStatusCode.OK, $$"""{"tag_name":"{{tag}}","html_url":"https://example.com"}"""), running);

        var release = await service.FindNewerReleaseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, release?.Version);
    }

    [Fact]
    public async Task FindNewerRelease_Found_LinksToThePageOfItsTagOnTheRepository()
    {
        using var service = Create(new StubHandler(HttpStatusCode.OK, """{"tag_name":"v5.2.0","html_url":"https://example.com/elsewhere"}"""), "5.1.0");

        var release = await service.FindNewerReleaseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://github.com/Der-Floh/Cubiomes-Viewer/releases/tag/v5.2.0"), release?.PageUrl);
    }

    [Fact]
    public async Task FindNewerRelease_AsksGitHubForItsNewestRelease()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"tag_name":"v5.1.0"}""");
        using var service = Create(handler, "5.1.0");

        await service.FindNewerReleaseAsync(TestContext.Current.CancellationToken);

        var request = Assert.IsType<HttpRequestMessage>(handler.Request);
        Assert.Equal(new Uri(Constants.LatestReleaseApiUrl), request.RequestUri);
        Assert.Contains("application/vnd.github+json", request.Headers.Accept.Select(accept => accept.MediaType));
        Assert.Contains(request.Headers.UserAgent, agent => agent.Product is { Name: "CubiomesViewer", Version: "5.1.0" });
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "not a release")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"API rate limit exceeded"}""")]
    public async Task FindNewerRelease_AnswerWithoutARelease_IsAnUpdateException(HttpStatusCode status, string body)
    {
        using var service = Create(new StubHandler(status, body), "5.1.0");

        await Assert.ThrowsAsync<UpdateException>(() => service.FindNewerReleaseAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindNewerRelease_OfADevelopmentBuild_IsRefused()
    {
        using var service = Create(new StubHandler(HttpStatusCode.OK, """{"tag_name":"v5.1.0"}"""), AppVersion.Unreleased);

        Assert.False(service.CanCheck);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FindNewerReleaseAsync(TestContext.Current.CancellationToken));
    }

    private static GitHubUpdateService Create(StubHandler handler, string running)
        => new(new HttpClient(handler), running, NullLogger<GitHubUpdateService>.Instance);

    /// <summary>Answers every request with one status and body, and keeps the last request.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
