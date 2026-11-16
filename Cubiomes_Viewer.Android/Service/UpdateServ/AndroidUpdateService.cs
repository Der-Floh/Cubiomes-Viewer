using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Android.Content;

using AndroidX.Core.Content;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.Logging;

using AndroidApplication = Android.App.Application;
using JavaFile = Java.IO.File;
using JavaIOException = Java.IO.IOException;

namespace Cubiomes_Viewer.Android.Service.UpdateServ;

/// <summary>
/// Updates the app from the APK of the newest release on GitHub. Android has the user confirm every installation, so a
/// newer release is offered, downloads into the app's cache once asked for and then goes to the system's installer.
/// </summary>
public sealed class AndroidUpdateService : IUpdateService, IDisposable
{
    /// <summary>The name of the APK among the files of a release, as the publish workflow uploads it.</summary>
    public const string PackageAssetName = "CubiomesViewer-android.apk";

    private const string UserAgentProduct = "CubiomesViewer";
    private const string GitHubJsonMediaType = "application/vnd.github+json";
    private const string PackageMimeType = "application/vnd.android.package-archive";
    private const string PartialFileSuffix = ".part";
    private const int CopyBufferSize = 81920;

    // The folder and the authority that Resources/xml/update_paths.xml and the manifest's file provider share with the installer.
    private const string DownloadFolderName = "updates";
    private const string FileProviderAuthoritySuffix = ".updates";

    private static readonly TimeSpan _releaseListTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly ILogger<AndroidUpdateService> _logger;
    private PackageAsset? _found;
    private string? _downloadedPath;

    public AndroidUpdateService(ILogger<AndroidUpdateService> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProduct, AppVersion.Current));
    }

    public string CurrentVersion => AppVersion.Current;
    public bool CanUpdate => AppVersion.IsRelease;
    public UpdateStyle Style => UpdateStyle.OnRequest;

    public async Task<AppRelease?> FindNewerReleaseAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfCannotUpdate();
        if (_downloadedPath is null)
            DeleteEarlierDownloads();

        try
        {
            _found = await ReadNewestPackageAsync(cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            throw new UpdateException(ex.Message, ex);
        }

        _logger.LogInformation("Newest release with an APK: {Version}", _found?.Release.Version ?? "none");
        return _found is { } package && IsNewer(package.Release.Version, CurrentVersion) ? package.Release : null;
    }

    public async Task DownloadAsync(AppRelease release, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ThrowIfCannotUpdate();
        if (_found is not { } package || package.Release != release)
            throw new InvalidOperationException("Only the release found last can be downloaded.");

        var path = Path.Combine(DownloadFolder, PackageAssetName);
        try
        {
            Directory.CreateDirectory(DownloadFolder);
            await DownloadToAsync(package.Url, path, progress, cancellationToken);
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            throw new UpdateException(ex.Message, ex);
        }

        _downloadedPath = path;
    }

    /// <summary>Does nothing, as Android installs a release only once the user confirms it.</summary>
    public void InstallOnExit()
    {
    }

    public void InstallAndRestart()
    {
        var path = _downloadedPath ?? throw new InvalidOperationException("No release is downloaded.");
        var context = AndroidApplication.Context;
        var package = FileProvider.GetUriForFile(context, context.PackageName + FileProviderAuthoritySuffix, new JavaFile(path));
        var install = new Intent(Intent.ActionView);
        install.SetDataAndType(package, PackageMimeType);
        install.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        context.StartActivity(install);
    }

    public void Dispose() => _http.Dispose();

    private static string DownloadFolder => Path.Combine(AndroidApplication.Context.CacheDir!.AbsolutePath, DownloadFolderName);

    private void ThrowIfCannotUpdate()
    {
        if (!CanUpdate)
            throw new InvalidOperationException("A development build does not update itself.");
    }

    /// <summary>The APK of the newest release, or <see langword="null"/> when that release has none.</summary>
    private async Task<PackageAsset?> ReadNewestPackageAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_releaseListTimeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, Constants.LatestReleaseApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubJsonMediaType));
        using var response = await _http.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var release = await JsonDocument.ParseAsync(content, cancellationToken: timeout.Token);
        return FindPackage(release.RootElement);
    }

    private static PackageAsset? FindPackage(JsonElement release)
    {
        var version = ReadString(release, "tag_name").TrimStart('v');
        foreach (var asset in Read(release, "assets", JsonValueKind.Array).EnumerateArray())
        {
            if (ReadString(asset, "name") != PackageAssetName)
                continue;

            long? size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var bytes) ? bytes : null;
            var url = new Uri(ReadString(asset, "browser_download_url"));
            return new PackageAsset(new AppRelease { Version = version, DownloadSize = size }, url);
        }

        return null;
    }

    private static string ReadString(JsonElement element, string name) => Read(element, name, JsonValueKind.String).GetString()!;

    private static JsonElement Read(JsonElement element, string name, JsonValueKind kind)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == kind
            ? value
            : throw new JsonException($"The release has no {name} of the kind {kind}.");

    private async Task DownloadToAsync(Uri url, string path, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var partialPath = path + PartialFileSuffix;
        using (var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(partialPath);
            await CopyAsync(source, target, response.Content.Headers.ContentLength, progress, cancellationToken);
        }

        File.Move(partialPath, path, overwrite: true);
    }

    private static async Task CopyAsync(Stream source, Stream target, long? length, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        var copied = 0L;
        var reported = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (length is not > 0)
                continue;

            var percent = (int)(copied * 100 / length.Value);
            if (percent != reported)
            {
                reported = percent;
                progress?.Report(percent);
            }
        }
    }

    /// <summary>Removes an APK that an earlier run downloaded, installed or not, as it only takes up space.</summary>
    private void DeleteEarlierDownloads()
    {
        try
        {
            if (Directory.Exists(DownloadFolder))
                Directory.Delete(DownloadFolder, recursive: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "The earlier download in {Folder} could not be removed", DownloadFolder);
        }
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

    /// <summary>Failures of the network, the disk or the release list, which a later check may not run into; a timeout counts, a cancellation does not.</summary>
    private static bool IsUnreachable(Exception exception, CancellationToken cancellationToken)
        => exception is HttpRequestException or WebException or IOException or JavaIOException or JsonException or UnauthorizedAccessException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private sealed record PackageAsset(AppRelease Release, Uri Url);
}
