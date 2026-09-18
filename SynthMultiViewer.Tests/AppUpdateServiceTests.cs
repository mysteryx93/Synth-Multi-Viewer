using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppUpdateServiceTests
{
    [Theory]
    [InlineData(UpdateInterval.Never, null, false)]
    [InlineData(UpdateInterval.Daily, null, true)]
    [InlineData(UpdateInterval.Daily, 0.5, false)]
    [InlineData(UpdateInterval.Daily, 1.5, true)]
    [InlineData(UpdateInterval.Biweekly, 3.0, false)]
    [InlineData(UpdateInterval.Biweekly, 4.0, true)]
    [InlineData(UpdateInterval.Weekly, 6.0, false)]
    [InlineData(UpdateInterval.Weekly, 8.0, true)]
    [InlineData(UpdateInterval.Bimonthly, 14.0, false)]
    [InlineData(UpdateInterval.Bimonthly, 16.0, true)]
    [InlineData(UpdateInterval.Monthly, 29.0, false)]
    [InlineData(UpdateInterval.Monthly, 31.0, true)]
    public async Task CheckForUpdatesAsync_Interval_QueriesWhenDue(
        UpdateInterval interval, double? daysAgo, bool shouldQuery)
    {
        var now = new DateTime(2026, 1, 15);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value =
            {
                CheckForUpdates = interval,
                LastCheckForUpdate = daysAgo is { } days ? now.AddDays(-days) : null
            }
        };
        var versions = new TestSupport.MemoryAppVersionClient { Result = new(new(1, 2, 3)) };
        var service = CreateService(settings, versions, now: now);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(shouldQuery ? 1 : 0, versions.QueryCount);
        if (shouldQuery)
        {
            Assert.Equal(now, settings.Value.LastCheckForUpdate);
            Assert.Equal(1, settings.SaveCount);
        }
        else
        {
            Assert.Equal(0, settings.SaveCount);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_QueryFailed_DoesNotSaveLastCheck()
    {
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new TestSupport.MemoryAppVersionClient();
        var service = CreateService(settings, versions);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(1, versions.QueryCount);
        Assert.Null(settings.Value.LastCheckForUpdate);
        Assert.Equal(0, settings.SaveCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_SameVersion_DoesNotPrompt()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new TestSupport.MemoryAppVersionClient { Result = new(new(1, 2, 3)) };
        var process = new TestSupport.MemoryProcessService();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(0, dialogs.FrameworkDialogCount);
        Assert.Null(process.LastUrl);
        Assert.Equal(1, settings.SaveCount);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionAccepted_OpensDownloadUrl()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.QueueFrameworkResult(true);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new TestSupport.MemoryAppVersionClient
        {
            Result = new(new(2, 0, 0), "https://example/download")
        };
        var process = new TestSupport.MemoryProcessService();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        var prompt = Assert.IsType<MessageBoxSettings>(dialogs.LastFrameworkSettings);
        Assert.Equal("Update Available!", prompt.Title);
        Assert.Equal(MessageBoxButton.YesNo, prompt.Button);
        Assert.Equal("https://example/download", process.LastUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionAcceptedWithoutUrl_OpensReleases()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.QueueFrameworkResult(true);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new TestSupport.MemoryAppVersionClient { Result = new(new(2, 0, 0)) };
        var process = new TestSupport.MemoryProcessService();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(AppUpdateService.ReleasesUrl, process.LastUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NewerVersionDeclined_DoesNotOpenUrl()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.QueueFrameworkResult(false);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { CheckForUpdates = UpdateInterval.Daily }
        };
        var versions = new TestSupport.MemoryAppVersionClient { Result = new(new(2, 0, 0)) };
        var process = new TestSupport.MemoryProcessService();
        var service = CreateService(settings, versions, dialogs, process);

        await service.CheckForUpdatesAsync(new WorkspaceViewModel());

        Assert.Equal(1, dialogs.FrameworkDialogCount);
        Assert.Null(process.LastUrl);
        Assert.Equal(1, settings.SaveCount);
    }

    private static AppUpdateService CreateService(
        TestSupport.MemorySettingsProvider settings,
        TestSupport.MemoryAppVersionClient versions,
        TestSupport.FakeDialogManager? dialogs = null,
        TestSupport.MemoryProcessService? process = null,
        DateTime? now = null)
    {
        var environment = new TestSupport.TestEnvironment { Now = now ?? new DateTime(2026, 1, 15) };
        return new(
            settings,
            TestSupport.CreateDialogs(manager: dialogs ?? new TestSupport.FakeDialogManager()),
            environment,
            versions,
            process ?? new TestSupport.MemoryProcessService());
    }
}
