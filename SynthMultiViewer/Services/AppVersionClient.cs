using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using HanumanInstitute.SynthMultiViewer.Models;

namespace HanumanInstitute.SynthMultiViewer.Services;

/// <inheritdoc />
public class AppVersionClient : IAppVersionClient
{
    /// <summary>
    /// Application id used by the store app-version API.
    /// </summary>
    public const string AppId = "synthmultiviewer";

    private const string QueryVersionUrl = "https://store.hanumaninstitute.com/api/app-version?app={0}&os={1}";

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a client that queries the store app-version API.
    /// </summary>
    public AppVersionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc />
    public async Task<AppVersionInfo?> QueryVersionAsync()
    {
        try
        {
            var url = QueryVersionUrl.FormatInvariant(AppId, GetRuntimeIdentifier());
            using var stream = await _httpClient.GetStreamAsync(url);
            return await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.AppVersionQuery);
        }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        catch (JsonException) { }
        return null;
    }

    /// <summary>
    /// Returns the runtime identifier sent as the <c>os</c> query value.
    /// </summary>
    internal static string GetRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        return string.Empty;
    }
}
