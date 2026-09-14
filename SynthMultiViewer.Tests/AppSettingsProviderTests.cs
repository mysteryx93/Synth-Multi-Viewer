using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class AppSettingsProviderTests
{
    [Fact]
    public void Load_MissingFile_ReturnsDefaultLightTheme()
    {
        using var file = new TemporaryConfig();
        var provider = CreateProvider(file.Path);

        var settings = provider.Load();

        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    [Fact]
    public void Save_RoundTrip_PersistsDarkTheme()
    {
        using var file = new TemporaryConfig();
        var provider = CreateProvider(file.Path);
        provider.Value.Theme = AppTheme.Dark;

        provider.Save();
        var loaded = CreateProvider(file.Path).Load();

        Assert.Equal(AppTheme.Dark, loaded.Theme);
    }

    [Fact]
    public void Save_RoundTrip_PersistsLibraryPaths()
    {
        using var file = new TemporaryConfig();
        var provider = CreateProvider(file.Path);
        provider.Value.VapourSynthPath = "/opt/vs";
        provider.Value.VapourSynthPluginFolders = "/opt/vs/plugins";
        provider.Value.VapourSynthReplacePlugins = true;
        provider.Value.AviSynthPath = "/opt/avs";
        provider.Value.AviSynthPluginFolders = "/opt/avs/plugins";
        provider.Value.VapourSynthThreads = 8;

        provider.Save();
        var loaded = CreateProvider(file.Path).Load();

        Assert.Equal("/opt/vs", loaded.VapourSynthPath);
        Assert.Equal("/opt/vs/plugins", loaded.VapourSynthPluginFolders);
        Assert.True(loaded.VapourSynthReplacePlugins);
        Assert.Equal("/opt/avs", loaded.AviSynthPath);
        Assert.Equal("/opt/avs/plugins", loaded.AviSynthPluginFolders);
        Assert.False(loaded.AviSynthReplacePlugins);
        Assert.Equal(8, loaded.VapourSynthThreads);
    }

    [Fact]
    public void Save_RoundTrip_PersistsWindowBounds()
    {
        using var file = new TemporaryConfig();
        var provider = CreateProvider(file.Path);
        provider.Value.Width = 1280;
        provider.Value.Height = 800;
        provider.Value.Maximized = true;

        provider.Save();
        var loaded = CreateProvider(file.Path).Load();

        Assert.Equal(1280, loaded.Width);
        Assert.Equal(800, loaded.Height);
        Assert.True(loaded.Maximized);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultSettings()
    {
        using var file = new TemporaryConfig();
        File.WriteAllText(file.Path, "{ not json");
        var provider = CreateProvider(file.Path);

        var settings = provider.Load();

        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    private static AppSettingsProvider CreateProvider(string path) =>
        new(new SerializationService(), new FixedAppPath(path));

    private sealed class FixedAppPath(string path) : IAppPathService
    {
        public string ConfigFile { get; } = path;
    }

    private sealed class TemporaryConfig : IDisposable
    {
        public TemporaryConfig()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"SynthMultiViewer-{Guid.NewGuid():N}.json");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
