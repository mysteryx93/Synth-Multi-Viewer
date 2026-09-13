using Avalonia.Media;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.SynthMultiViewer.Models;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabColorsTests
{
    [Fact]
    public void For_LightTheme_EditorIsNearWhiteAndViewerHasMoreHue()
    {
        var editor = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light);
        var viewer = TabColors.For(ScriptKind.VapourSynth, true, AppTheme.Light);

        Assert.Equal(TabColors.Mix(TabColors.VapourSynth, false, false), editor);
        Assert.True(Distance(editor, Color.FromRgb(0xF3, 0xF3, 0xF3)) <
                    Distance(viewer, Color.FromRgb(0xF3, 0xF3, 0xF3)) * 0.5);
        Assert.True(Distance(editor, Color.FromRgb(0xF3, 0xF3, 0xF3)) < 25);
    }

    [Fact]
    public void For_DarkTheme_EditorIsNearBlackAndViewerHasMoreHue()
    {
        var editor = TabColors.For(ScriptKind.AviSynth, false, AppTheme.Dark);
        var viewer = TabColors.For(ScriptKind.AviSynth, true, AppTheme.Dark);

        var chrome = Color.FromRgb(0x20, 0x20, 0x20);
        Assert.True(Distance(editor, chrome) < Distance(viewer, chrome) * 0.35);
        Assert.True(editor.B > editor.G);
        Assert.True(viewer.B - viewer.G > editor.B - editor.G);
        Assert.True(Math.Max(editor.R, Math.Max(editor.G, editor.B)) < 40);
        Assert.True(Math.Max(viewer.R, Math.Max(viewer.G, viewer.B)) < 72);
    }

    [Fact]
    public void For_Engines_KeepDistinctHues()
    {
        var vs = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light);
        var avs = TabColors.For(ScriptKind.AviSynth, false, AppTheme.Light);

        Assert.True(vs.G > vs.B);
        Assert.True(avs.B > avs.G);
    }

    [Fact]
    public void For_DarkTheme_EnginesKeepDistinctHues()
    {
        var vsEditor = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Dark);
        var avsEditor = TabColors.For(ScriptKind.AviSynth, false, AppTheme.Dark);
        var vsViewer = TabColors.For(ScriptKind.VapourSynth, true, AppTheme.Dark);
        var avsViewer = TabColors.For(ScriptKind.AviSynth, true, AppTheme.Dark);

        Assert.True(vsEditor.G > vsEditor.B);
        Assert.True(avsEditor.B > avsEditor.G);
        Assert.True(vsViewer.G - vsViewer.B > vsEditor.G - vsEditor.B);
        Assert.True(avsViewer.B - avsViewer.G > avsEditor.B - avsEditor.G);
    }

    [Fact]
    public void For_CustomHue_MixesInsteadOfUsingRawColor()
    {
        var mixed = TabColors.For(ScriptKind.VapourSynth, false, AppTheme.Light, Colors.HotPink);

        Assert.Equal(TabColors.Mix(Colors.HotPink, false, false), mixed);
        Assert.NotEqual(Colors.HotPink, mixed);
    }

    private static double Distance(Color a, Color b)
    {
        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}
