using System.Net;
using System.Reactive.Linq;
using System.Text;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppVersionClientTests
{
    [Fact]
    public async Task QueryVersionAsync_RequestsSynthMultiViewerForCurrentOs()
    {
        var handler = new StubHandler
        {
            Response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"LatestVersion":"1.3","DownloadUrl":"https://example/download"}""",
                    Encoding.UTF8, "application/json")
            }
        };
        var client = new AppVersionClient(new(handler));

        var version = await client.QueryVersionAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("store.hanumaninstitute.com", handler.LastRequest!.Host);
        Assert.Equal("/api/app-version", handler.LastRequest.AbsolutePath);
        Assert.Contains("app=synthmultiviewer", handler.LastRequest.Query, StringComparison.Ordinal);
        Assert.Contains("os=" + AppVersionClient.GetRuntimeIdentifier(), handler.LastRequest.Query,
            StringComparison.Ordinal);
        Assert.Equal(new(1, 3), version!.LatestVersion);
        Assert.Equal("https://example/download", version.DownloadUrl);
    }

    [Fact]
    public async Task QueryVersionAsync_HttpFailure_ReturnsNull()
    {
        var handler = new StubHandler
        {
            Response = new(HttpStatusCode.NotFound)
        };
        var client = new AppVersionClient(new(handler));

        var version = await client.QueryVersionAsync();

        Assert.Null(version);
    }

    [Fact]
    public async Task QueryVersionAsync_InvalidJson_ReturnsNull()
    {
        var handler = new StubHandler
        {
            Response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            }
        };
        var client = new AppVersionClient(new(handler));

        var version = await client.QueryVersionAsync();

        Assert.Null(version);
    }

    [Fact]
    public async Task CheckForUpdates_NewerVersion_ShowsAvailable()
    {
        var versions = new TestSupport.MemoryAppVersionClient
        {
            Result = new(new(2, 0, 0))
        };
        var model = TestSupport.CreateHelp(versions: versions);

        await model.CheckForUpdates.Execute();

        Assert.Equal("v2.0.0 is available!", model.CheckForUpdateText);
    }

    [Fact]
    public async Task CheckForUpdates_SameOrOlderVersion_ShowsLatest()
    {
        var versions = new TestSupport.MemoryAppVersionClient
        {
            Result = new(new(1, 2, 3))
        };
        var model = TestSupport.CreateHelp(versions: versions);

        await model.CheckForUpdates.Execute();

        Assert.Equal("You have the latest version", model.CheckForUpdateText);
    }

    [Fact]
    public async Task CheckForUpdates_QueryFailed_KeepsCheckingText()
    {
        var model = TestSupport.CreateHelp(versions: new TestSupport.MemoryAppVersionClient());

        await model.CheckForUpdates.Execute();

        Assert.Equal("Checking for updates...", model.CheckForUpdateText);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Uri? LastRequest { get; private set; }
        public required HttpResponseMessage Response { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;
            return Task.FromResult(Response);
        }
    }
}
