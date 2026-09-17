using System.Reactive.Linq;
using Avalonia.Headless.XUnit;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class DefaultScriptServiceTests
{
    [AvaloniaFact]
    public void VapourSynth_Asset_ContainsBlankClipAndOutput()
    {
        var scripts = new DefaultScriptService();

        var script = scripts.VapourSynth;

        Assert.Contains("import vapoursynth as vs", script, StringComparison.Ordinal);
        Assert.Contains("BlankClip", script, StringComparison.Ordinal);
        Assert.Contains("set_output()", script, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AviSynth_Asset_ContainsBlankClip()
    {
        var scripts = new DefaultScriptService();

        var script = scripts.AviSynth;

        Assert.Contains("BlankClip", script, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task New_Executed_UsesDefaultVapourSynthScript()
    {
        var defaults = new TestSupport.MemoryDefaultScripts();
        var model = new MainViewModel(
            TestSupport.CreateDialogs(), new TestSupport.TestEnvironment(), defaults,
            new TestSupport.MemorySettingsProvider(), new TestSupport.MemoryFrameworkDetection());

        await model.New.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(defaults.VapourSynth, editor.Script);
        Assert.Equal(ScriptKind.VapourSynth, editor.Kind);
    }

    [AvaloniaFact]
    public async Task NewAviSynth_Executed_UsesDefaultAviSynthScript()
    {
        var defaults = new TestSupport.MemoryDefaultScripts();
        var model = new MainViewModel(
            TestSupport.CreateDialogs(), new TestSupport.TestEnvironment(), defaults,
            new TestSupport.MemorySettingsProvider(), new TestSupport.MemoryFrameworkDetection());

        await model.NewAviSynth.Execute();

        var editor = Assert.IsType<EditorViewModel>(Assert.Single(model.ScriptList));
        Assert.Equal(defaults.AviSynth, editor.Script);
        Assert.Equal(ScriptKind.AviSynth, editor.Kind);
    }
}
