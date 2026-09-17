using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptAssistServiceTests
{
    [Fact]
    public void IsEnabled_FollowsEnhanceEditorSetting()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var service = new ScriptAssistService(settings);

        Assert.True(service.IsEnabled);
        settings.Value.EnhanceEditorWithAutoComplete = false;
        Assert.False(service.IsEnabled);
        settings.Value.EnhanceEditorWithAutoComplete = true;
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void Configure_WhenDisabled_DoesNotEnumerateNativeCatalogs()
    {
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };
        var service = new ScriptAssistService(settings);

        service.Configure(nameof(ScriptKind.VapourSynth), "a");
        service.Configure(nameof(ScriptKind.AviSynth), "b");
        service.Refresh();

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void VapourSynthIncludeReadsHavsfuncFromSitePackages()
    {
        var file = ScriptIncludeIO.VapourSynth("havsfunc", null);
        Assert.SkipWhen(file == null, "havsfunc is not installed on this machine");
        Assert.Contains("def QTGMC(", file.Value.Text, StringComparison.Ordinal);
        Assert.True(file.Value.Path.Contains("site-packages", StringComparison.Ordinal)
            || file.Value.Path.Contains("dist-packages", StringComparison.Ordinal)
            || File.Exists(file.Value.Path));
        Assert.NotNull(VsPathResolver.GetPythonModuleDirectories()
            .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "havsfunc.py"))
                || Directory.Exists(Path.Combine(directory, "havsfunc"))));
    }
}
