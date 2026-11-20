using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Cubiomes_Viewer.Data;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>Compares the running version with the newest release on GitHub, for the desktop and Android apps.</summary>
public sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private const string UserAgentProduct = "CubiomesViewer";
    private const string GitHubJsonMediaType = "application/vnd.github+json";
    private const string ReleasePagePath = "/releases/tag/";
    private const char TagPrefix = 'v';

    private static readonly TimeSpan _releaseTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly ILogger<GitHubUpdateService> _logger;

    public GitHubUpdateService(ILogger<GitHubUpdateService> logger) : this(new HttpClient(), AppVersion.Current, logger)
    {
    }

    internal GitHubUpdateService(HttpClient http, string currentVersion, ILogger<GitHubUpdateService> logger)
    {
        _http = http;
        _logger = logger;
        CurrentVersion = currentVersion;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProduct, currentVersion));
    }

    public string CurrentVersion { get; }
    public bool CanCheck => CurrentVersion != AppVersion.Unreleased;

    public async Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        if (!CanCheck)
            throw new InvalidOperationException("A development build has no version to compare with the releases.");

        AppRelease newest;
        try
        {
            newest = await ReadNewestReleaseAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            throw new UpdateException(ex.Message, ex);
        }

        _logger.LogInformation("Newest release: {Version}", newest.Version);
        return IsNewer(newest.Version, CurrentVersion) ? newest : null;
    }

    public void Dispose() => _http.Dispose();

    private async Task<AppRelease> ReadNewestReleaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_releaseTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, Constants.LatestReleaseApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubJsonMediaType));
        using var response = await _http.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var release = await JsonDocument.ParseAsync(content, cancellationToken: timeout.Token);
        return ReadRelease(release.RootElement);
    }

    /// <summary>The version of the release and the address of its page, both from its tag.</summary>
    private static AppRelease ReadRelease(JsonElement release)
    {
        var tag = release.ValueKind == JsonValueKind.Object && release.TryGetProperty("tag_name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString()!
            : throw new JsonException("The release has no tag_name.");
        return new AppRelease { Version = tag.TrimStart(TagPrefix), PageUrl = new Uri($"{Constants.RepositoryUrl}{ReleasePagePath}{Uri.EscapeDataString(tag)}") };
    }

    /// <summary>Whether <paramref name="candidate"/> is a later version, a release counting as later than its own prereleases.</summary>
    private static bool IsNewer(string candidate, string current)
    {
        if (!TryParseVersion(candidate, out var candidateNumber, out var isCandidatePrerelease)
            || !TryParseVersion(current, out var currentNumber, out var isCurrentPrerelease))
            return false;

        var order = candidateNumber.CompareTo(currentNumber);
        return order > 0 || (order == 0 && isCurrentPrerelease && !isCandidatePrerelease);
    }

    private static bool TryParseVersion(string text, out Version number, out bool isPrerelease)
    {
        var withoutMetadata = text.Split('+')[0];
        var dash = withoutMetadata.IndexOf('-', StringComparison.Ordinal);
        isPrerelease = dash >= 0;
        var parsed = Version.TryParse(isPrerelease ? withoutMetadata[..dash] : withoutMetadata, out var version);
        number = version ?? new Version();
        return parsed;
    }

    /// <summary>Failures of the network or the answer, which a later check may not run into; a timeout counts, a cancellation does not.</summary>
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or WebException or IOException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
}
