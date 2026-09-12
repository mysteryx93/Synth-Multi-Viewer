using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using HanumanInstitute.SynthMultiViewer.Views;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void Ok_DarkThemeSelected_SavesAndAppliesTheme()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var theme = new TestSupport.MemoryAppTheme();
        var model = new SettingsViewModel(settings, theme, new TestSupport.MemoryFrameworkDetection());
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal("Dark", theme.RequestedTheme);
        Assert.Equal(1, settings.SaveCount);
        Assert.True(model.DialogResult);
    }

    [Fact]
    public void Cancel_ThemeChanged_DoesNotSave()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { Theme = AppTheme.Light } };
        var theme = new TestSupport.MemoryAppTheme();
        var model = new SettingsViewModel(settings, theme, new TestSupport.MemoryFrameworkDetection());
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Cancel).Execute(null);

        Assert.Equal(AppTheme.Light, settings.Value.Theme);
        Assert.Equal("Light", theme.RequestedTheme);
        Assert.Equal(0, settings.SaveCount);
        Assert.Null(model.DialogResult);
    }

    [Fact]
    public void Apply_DarkThemeSelected_SavesWithoutClosing()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var theme = new TestSupport.MemoryAppTheme();
        var model = new SettingsViewModel(settings, theme, new TestSupport.MemoryFrameworkDetection());
        var closed = false;
        model.RequestClose += (_, _) => closed = true;
        model.Theme = AppTheme.Dark;

        ((ICommand)model.Apply).Execute(null);

        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal("Dark", theme.RequestedTheme);
        Assert.Equal(1, settings.SaveCount);
        Assert.False(closed);
        Assert.Null(model.DialogResult);
    }

    [Fact]
    public void RestoreDefault_DarkThemeSelected_ResetsWorkingCopy()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { Theme = AppTheme.Dark } };
        var model = new SettingsViewModel(
            settings, new TestSupport.MemoryAppTheme(), new TestSupport.MemoryFrameworkDetection())
        {
            Theme = AppTheme.Dark
        };

        ((ICommand)model.RestoreDefault).Execute(null);

        Assert.Equal(AppTheme.Light, model.Theme);
        Assert.Equal("", model.VapourSynthPath);
        Assert.Equal("", model.VapourSynthPluginFolders);
        Assert.False(model.VapourSynthReplacePlugins);
        Assert.Equal("", model.AviSynthPath);
        Assert.Equal("", model.AviSynthPluginFolders);
        Assert.False(model.AviSynthReplacePlugins);
        Assert.Equal(Environment.ProcessorCount, model.VapourSynthThreads);
        Assert.Equal(AppTheme.Dark, settings.Value.Theme);
        Assert.Equal(0, settings.SaveCount);
    }

    [AvaloniaFact]
    public void SettingsView_DarkTheme_SelectsDarkComboItem()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { Theme = AppTheme.Dark } };
        var model = new SettingsViewModel(settings, new TestSupport.MemoryAppTheme(), new TestSupport.MemoryFrameworkDetection());
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var combo = view.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.Equal(AppTheme.Dark, combo.SelectedItem);
    }

    [AvaloniaFact]
    public void SettingsView_DialogButtons_CenterLabelInsidePadding()
    {
        var model = new SettingsViewModel(
            new TestSupport.MemorySettingsProvider(),
            new TestSupport.MemoryAppTheme(),
            new TestSupport.MemoryFrameworkDetection());
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var buttons = view.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Content is "Default" or "Apply" or "OK" or "Cancel")
            .ToList();

        Assert.Equal(4, buttons.Count);
        Assert.All(buttons, button =>
        {
            Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, button.VerticalContentAlignment);
            Assert.True(button.Padding.Top >= 4);
            Assert.True(button.Padding.Bottom >= 4);
            Assert.True(button.Bounds.Height >= 28);
        });
    }

    [AvaloniaFact]
    public void SettingsView_FrameworkStatus_ShowsDetectedAndNotFoundCaptions()
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(true, "/usr/lib/libvsscript.so", ["/usr/lib/vapoursynth", "/usr/lib64/vapoursynth"]),
            AviSynth = new(false)
        };
        var model = new SettingsViewModel(
            new TestSupport.MemorySettingsProvider(), new TestSupport.MemoryAppTheme(), detection);
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var vapourSynth = view.FindControl<TextBlock>("VapourSynthStatus")!;
        var aviSynth = view.FindControl<TextBlock>("AviSynthStatus")!;
        var detectedLibrary = view.FindControl<TextBox>("VapourSynthDetectedLibrary")!;
        var detectedPlugins = view.FindControl<TextBox>("VapourSynthDetectedPlugins")!;
        var library = view.FindControl<TextBox>("VapourSynthLibraryPath")!;
        var plugins = view.FindControl<TextBox>("VapourSynthPluginFolders")!;

        Assert.Equal("(Detected)", vapourSynth.Text);
        Assert.Equal("(Not Found)", aviSynth.Text);
        var vapourSynthTitle = view.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == "VapourSynth");
        Assert.True(vapourSynth.Bounds.Left > vapourSynthTitle.Bounds.Right);
        Assert.True(vapourSynth.Bounds.Left < vapourSynthTitle.Bounds.Right + 80);
        Assert.True(detectedLibrary.IsReadOnly);
        Assert.Equal("/usr/lib/libvsscript.so", detectedLibrary.Text);
        Assert.Equal("/usr/lib/vapoursynth; /usr/lib64/vapoursynth", detectedPlugins.Text);
        Assert.Equal("Override detected library", library.PlaceholderText);
        Assert.Equal("Extra folders, separated by ;", plugins.PlaceholderText);
        Assert.Equal("", model.AviSynthLibraryDisplay);
        Assert.Equal("", model.AviSynthPluginsDisplay);
    }

    [Fact]
    public void Constructor_FrameworkStatus_UsesDetectionResults()
    {
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value =
            {
                VapourSynthPath = "/opt/vs",
                VapourSynthPluginFolders = "/opt/vs/plugins",
                VapourSynthReplacePlugins = true,
                AviSynthPath = "/opt/avs",
                AviSynthPluginFolders = "/opt/avs/plugins"
            }
        };
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(true, "/opt/vs/libvsscript.so"),
            AviSynth = new(false)
        };

        var model = new SettingsViewModel(settings, new TestSupport.MemoryAppTheme(), detection);

        Assert.True(model.VapourSynthFound);
        Assert.False(model.AviSynthFound);
        Assert.Equal("/opt/vs", model.VapourSynthPath);
        Assert.Equal("/opt/vs/plugins", model.VapourSynthPluginFolders);
        Assert.True(model.VapourSynthReplacePlugins);
        Assert.Equal("/opt/avs", model.AviSynthPath);
        Assert.Equal("/opt/avs/plugins", model.AviSynthPluginFolders);
        Assert.Equal("/opt/vs/libvsscript.so", model.VapourSynthLibraryDisplay);
        Assert.Equal("(Detected)", model.VapourSynthStatus);
        Assert.Equal("(Not Found)", model.AviSynthStatus);
    }

    [Fact]
    public void Constructor_ConfiguredPathMissing_ShowsErrorStatus()
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(FrameworkStatus.Error)
        };

        var model = new SettingsViewModel(
            new TestSupport.MemorySettingsProvider(), new TestSupport.MemoryAppTheme(), detection);

        Assert.Equal("(Error)", model.VapourSynthStatus);
        Assert.False(model.VapourSynthFound);
    }

    [Fact]
    public void Ok_LibraryPathsEntered_SavesAndAppliesPaths()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var detection = new TestSupport.MemoryFrameworkDetection();
        var model = new SettingsViewModel(settings, new TestSupport.MemoryAppTheme(), detection)
        {
            VapourSynthPath = " /opt/vs ",
            VapourSynthPluginFolders = " /opt/vs/plugins ",
            VapourSynthReplacePlugins = true,
            AviSynthPath = "/opt/avs",
            AviSynthPluginFolders = "/opt/avs/plugins"
        };

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal("/opt/vs", settings.Value.VapourSynthPath);
        Assert.Equal("/opt/vs/plugins", settings.Value.VapourSynthPluginFolders);
        Assert.True(settings.Value.VapourSynthReplacePlugins);
        Assert.Equal("/opt/avs", settings.Value.AviSynthPath);
        Assert.Equal("/opt/avs/plugins", settings.Value.AviSynthPluginFolders);
        Assert.Equal("/opt/vs", detection.LastApplied!.VapourSynthPath);
        Assert.Equal("/opt/avs", detection.LastApplied.AviSynthPath);
        Assert.True(detection.LastApplied.VapourSynthReplacePlugins);
    }

    [Fact]
    public void Ok_VapourSynthThreadsEntered_SavesThreadCount()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var model = new SettingsViewModel(
            settings, new TestSupport.MemoryAppTheme(), new TestSupport.MemoryFrameworkDetection())
        {
            VapourSynthThreads = 4
        };

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(4, settings.Value.VapourSynthThreads);
    }

    [Fact]
    public void Constructor_ZeroVapourSynthThreads_UsesProcessorCount()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 0 } };

        var model = new SettingsViewModel(
            settings, new TestSupport.MemoryAppTheme(), new TestSupport.MemoryFrameworkDetection());

        Assert.Equal(Environment.ProcessorCount, model.VapourSynthThreads);
    }

    [AvaloniaFact]
    public void SettingsView_VapourSynthThreads_ShowsNumericUpDownWithoutSpinner()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 6 } };
        var model = new SettingsViewModel(
            settings, new TestSupport.MemoryAppTheme(), new TestSupport.MemoryFrameworkDetection());
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var box = view.FindControl<NumericUpDown>("VapourSynthThreads")!;

        Assert.Equal(6, box.Value);
        Assert.Equal(1, box.Minimum);
        Assert.Equal(256, box.Maximum);
        Assert.False(box.ShowButtonSpinner);

        box.Value = null;
        box.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

        Assert.Equal(6, box.Value);
        Assert.Equal(6, model.VapourSynthThreads);
        Assert.False(DataValidationErrors.GetHasErrors(box));
    }
}
