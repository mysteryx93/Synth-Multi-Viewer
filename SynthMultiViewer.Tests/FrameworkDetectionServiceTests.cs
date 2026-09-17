using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.SynthMultiViewer.Services;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FrameworkDetectionServiceTests
{
    [Fact]
    public void Apply_ConfiguredPathMissing_ReportsErrorWithLoadMessage()
    {
        var missingVs = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libvsscript.so");
        var missingAvs = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "libavisynth.so");
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value =
            {
                VapourSynthPath = missingVs,
                AviSynthPath = missingAvs
            }
        };

        try
        {
            var detection = new FrameworkDetectionService(settings);

            Assert.Equal(FrameworkStatus.Error, detection.VapourSynth.Status);
            Assert.False(string.IsNullOrWhiteSpace(detection.VapourSynth.Message));
            Assert.Contains(missingVs, detection.VapourSynth.Message, StringComparison.Ordinal);
            Assert.Equal(FrameworkStatus.Error, detection.AviSynth.Status);
            Assert.False(string.IsNullOrWhiteSpace(detection.AviSynth.Message));
            Assert.Contains(missingAvs, detection.AviSynth.Message, StringComparison.Ordinal);
        }
        finally
        {
            ResetNativePaths();
        }
    }

    [Fact]
    public void Apply_WritesFactoryEnablementFromSettings()
    {
        var factory = new ScriptLanguageFactory(() => [], () => []);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };

        try
        {
            var detection = new FrameworkDetectionService(settings, factory);
            Assert.False(factory.IsEnabled);

            settings.Value.EnhanceEditorWithAutoComplete = true;
            detection.Apply(settings.Value);
            Assert.True(factory.IsEnabled);
        }
        finally
        {
            ResetNativePaths();
        }
    }

    private static void ResetNativePaths()
    {
        VsHelper.SetDllPath("");
        VsHelper.SetPluginFolders(null, false);
        AvsScript.SetDllPath(null);
        AvsScript.SetPluginFolders(null, false);
    }
}
