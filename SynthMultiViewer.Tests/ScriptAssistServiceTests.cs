using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptAssistServiceTests
{
    [Fact]
    public void Create_WellKnownLanguages_AreRegistered()
    {
        var service = new ScriptAssistService(new TestSupport.MemorySettingsProvider());

        Assert.True(service.IsEnabled);
        Assert.NotNull(service.Create(ScriptLanguageFactory.VapourSynth));
        Assert.NotNull(service.Create(ScriptLanguageFactory.AviSynth));
    }

    [Fact]
    public void Constructor_ReadsEnhanceEditorSetting()
    {
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };

        var service = new ScriptAssistService(settings);

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void EnhanceEditorSetting_ChangedAfterConstruct_DoesNotEnable()
    {
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };
        var service = new ScriptAssistService(settings);

        settings.Value.EnhanceEditorWithAutoComplete = true;

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void Configure_WhenDisabled_DoesNotEnumerateNativeCatalogs()
    {
        var service = new ScriptAssistService(new TestSupport.MemorySettingsProvider()) { IsEnabled = false };

        service.Configure(ScriptLanguageFactory.VapourSynth, "a");
        service.Configure(ScriptLanguageFactory.AviSynth, "b");
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
