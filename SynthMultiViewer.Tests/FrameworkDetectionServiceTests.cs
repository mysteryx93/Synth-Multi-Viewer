using HanumanInstitute.ApiAviSynth;
using HanumanInstitute.ApiVapourSynth;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Services;
using Moq;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FrameworkDetectionServiceTests
{
    [Fact]
    public void Apply_ConfiguredPathMissing_ReportsErrorWithLoadMessage()
    {
        const string missingVs = "/missing/libvsscript.so";
        const string missingAvs = "/missing/libavisynth.so";
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
            var detection = new FrameworkDetectionService(settings, new FakeFileSystemService());

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
        var vs = new Mock<IVapourSynthNativeCatalog>();
        vs.Setup(n => n.Read()).Returns([]);
        var avs = new Mock<IAviSynthNativeCatalog>();
        avs.Setup(n => n.Read()).Returns([]);
        var folders = new Mock<IScriptDirectory>();
        folders.Setup(d => d.Roots()).Returns([]);
        folders.Setup(d => d.Files(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns([]);
        folders.Setup(d => d.TryRead(It.IsAny<string>())).Returns((string?)null);
        var factory = new ScriptLanguageFactory(vs.Object, avs.Object, folders.Object);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };
        try
        {
            var detection = new FrameworkDetectionService(settings, new FakeFileSystemService(), factory);
            var disabled = factory.IsEnabled;
            settings.Value.EnhanceEditorWithAutoComplete = true;

            detection.Apply(settings.Value);

            Assert.False(disabled);
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
