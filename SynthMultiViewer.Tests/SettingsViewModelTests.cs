using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;
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
        var model = TestSupport.CreateSettings(settings, theme);
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
        var model = TestSupport.CreateSettings(settings, theme);
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
        var model = TestSupport.CreateSettings(settings, theme);
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
        var model = TestSupport.CreateSettings(settings);
        model.Theme = AppTheme.Dark;

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
        var model = TestSupport.CreateSettings(settings);
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var combo = view.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.Equal(AppTheme.Dark, combo.SelectedItem);
    }

    [AvaloniaFact]
    public void SettingsView_DialogButtons_CenterLabelInsidePadding()
    {
        var model = TestSupport.CreateSettings();
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
            VapourSynth = new(
                FrameworkStatus.Detected, "/usr/lib/libvsscript.so",
                ["/usr/lib/vapoursynth", "/usr/lib64/vapoursynth"], Version: "R79"),
            AviSynth = new(false)
        };
        var model = TestSupport.CreateSettings(detection: detection);
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var vapourSynth = view.FindControl<TextBlock>("VapourSynthStatus")!;
        var aviSynth = view.FindControl<TextBlock>("AviSynthStatus")!;
        var detectedLibrary = view.FindControl<TextBox>("VapourSynthDetectedLibrary")!;
        var detectedPlugins = view.FindControl<TextBox>("VapourSynthDetectedPlugins")!;
        var library = view.FindControl<TextBox>("VapourSynthLibraryPath")!;
        var plugins = view.FindControl<TextBox>("VapourSynthPluginFolders")!;

        Assert.Equal("(Detected R79)", vapourSynth.Text);
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
            VapourSynth = new(FrameworkStatus.Detected, "/opt/vs/libvsscript.so", Version: "R65"),
            AviSynth = new(false)
        };

        var model = TestSupport.CreateSettings(settings, detection: detection);

        Assert.True(model.VapourSynthFound);
        Assert.False(model.AviSynthFound);
        Assert.Equal("/opt/vs", model.VapourSynthPath);
        Assert.Equal("/opt/vs/plugins", model.VapourSynthPluginFolders);
        Assert.True(model.VapourSynthReplacePlugins);
        Assert.Equal("/opt/avs", model.AviSynthPath);
        Assert.Equal("/opt/avs/plugins", model.AviSynthPluginFolders);
        Assert.Equal("/opt/vs/libvsscript.so", model.VapourSynthLibraryDisplay);
        Assert.Equal("(Detected R65)", model.VapourSynthStatus);
        Assert.Equal("(Not Found)", model.AviSynthStatus);
    }

    [Fact]
    public void Constructor_DetectedVersionDetail_ShowsCompactStatusAndFullTip()
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(
                FrameworkStatus.Detected, "/usr/lib/libvsscript.so", Version: "R79",
                VersionDetail: "VapourSynth Video Processing Library\nCore R79"),
            AviSynth = new(
                FrameworkStatus.Detected, "/usr/lib/libavisynth.so", Version: "AviSynth+ 3.7.5",
                VersionDetail: "AviSynth+ 3.7.5 (r4228, 3.7, x86_64)")
        };

        var model = TestSupport.CreateSettings(detection: detection);

        Assert.Equal("(Detected R79)", model.VapourSynthStatus);
        Assert.Contains("Core R79", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Detected AviSynth+ 3.7.5)", model.AviSynthStatus);
        Assert.Contains("r4228", model.AviSynthStatusTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_ConfiguredPathMissing_ShowsErrorStatusAndTip()
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(
                FrameworkStatus.Error, null, null, "Could not load VapourSynth from '/missing/vs'."),
            AviSynth = new(FrameworkStatus.Error)
        };

        var model = TestSupport.CreateSettings(detection: detection);

        Assert.Equal("(Error)", model.VapourSynthStatus);
        Assert.False(model.VapourSynthFound);
        Assert.Contains("/missing/vs", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Error)", model.AviSynthStatus);
        Assert.False(model.AviSynthFound);
        Assert.Contains("could not be loaded", model.AviSynthStatusTip, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_LibraryFailsToRun_ShowsErrorStatusAndTip()
    {
        var detection = new TestSupport.MemoryFrameworkDetection
        {
            VapourSynth = new(
                FrameworkStatus.Error, "/vs/VSScript.dll", null,
                "expected str, bytes or os.PathLike object, not NoneType"),
            AviSynth = new(
                FrameworkStatus.Error, "/avs/AviSynth.dll", null,
                "AviSynth could not evaluate the script.")
        };

        var model = TestSupport.CreateSettings(detection: detection);

        Assert.Equal("(Error)", model.VapourSynthStatus);
        Assert.False(model.VapourSynthFound);
        Assert.Contains("PathLike", model.VapourSynthStatusTip, StringComparison.Ordinal);
        Assert.Equal("(Error)", model.AviSynthStatus);
        Assert.False(model.AviSynthFound);
        Assert.Contains("evaluate", model.AviSynthStatusTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Ok_LibraryPathsEntered_SavesAndAppliesPaths()
    {
        var settings = new TestSupport.MemorySettingsProvider();
        var detection = new TestSupport.MemoryFrameworkDetection();
        var model = TestSupport.CreateSettings(settings, detection: detection);
        model.VapourSynthPath = " /opt/vs ";
        model.VapourSynthPluginFolders = " /opt/vs/plugins ";
        model.VapourSynthReplacePlugins = true;
        model.AviSynthPath = "/opt/avs";
        model.AviSynthPluginFolders = "/opt/avs/plugins";

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
        var model = TestSupport.CreateSettings(settings);
        model.VapourSynthThreads = 4;

        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(4, settings.Value.VapourSynthThreads);
    }

    [Fact]
    public void Constructor_ZeroVapourSynthThreads_UsesProcessorCount()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 0 } };

        var model = TestSupport.CreateSettings(settings);

        Assert.Equal(Environment.ProcessorCount, model.VapourSynthThreads);
    }

    [AvaloniaFact]
    public void SettingsView_VapourSynthThreads_ShowsNumericUpDownWithoutSpinner()
    {
        var settings = new TestSupport.MemorySettingsProvider { Value = { VapourSynthThreads = 6 } };
        var model = TestSupport.CreateSettings(settings);
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

    [Fact]
    public async Task BrowseVapourSynthLibrary_Select_SetsPath()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFile("/opt/vs/libvsscript.so");
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));

        await model.BrowseVapourSynthLibrary.Execute();

        Assert.Equal("/opt/vs/libvsscript.so", model.VapourSynthPath);
        Assert.IsType<OpenFileDialogSettings>(dialogs.LastFrameworkSettings);
        Assert.Equal(1, dialogs.FrameworkDialogCount);
    }

    [Fact]
    public async Task BrowseVapourSynthLibrary_Cancel_KeepsPath()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFile(null);
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));
        model.VapourSynthPath = "/keep";

        await model.BrowseVapourSynthLibrary.Execute();

        Assert.Equal("/keep", model.VapourSynthPath);
        Assert.Equal(1, dialogs.FrameworkDialogCount);
    }

    [Fact]
    public async Task BrowseVapourSynthPlugins_Select_AppendsFolder()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFolder("/opt/vs/plugins2");
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));
        model.VapourSynthPluginFolders = "/opt/vs/plugins";

        await model.BrowseVapourSynthPlugins.Execute();

        Assert.Equal("/opt/vs/plugins; /opt/vs/plugins2", model.VapourSynthPluginFolders);
        Assert.IsType<OpenFolderDialogSettings>(dialogs.LastFrameworkSettings);
    }

    [Fact]
    public async Task BrowseVapourSynthPlugins_SelectExisting_DoesNotDuplicate()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFolder("/opt/vs/plugins");
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));
        model.VapourSynthPluginFolders = "/opt/vs/plugins";

        await model.BrowseVapourSynthPlugins.Execute();

        Assert.Equal("/opt/vs/plugins", model.VapourSynthPluginFolders);
    }

    [Fact]
    public async Task BrowseAviSynthLibrary_Select_SetsPath()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFile("/opt/avs/libavisynth.so");
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));

        await model.BrowseAviSynthLibrary.Execute();

        Assert.Equal("/opt/avs/libavisynth.so", model.AviSynthPath);
        Assert.IsType<OpenFileDialogSettings>(dialogs.LastFrameworkSettings);
    }

    [Fact]
    public async Task BrowseAviSynthPlugins_Select_AppendsFolder()
    {
        var dialogs = new TestSupport.FakeDialogManager();
        dialogs.ReturnFolder("/opt/avs/plugins");
        var model = TestSupport.CreateSettings(dialogs: TestSupport.CreateDialogs(manager: dialogs));

        await model.BrowseAviSynthPlugins.Execute();

        Assert.Equal("/opt/avs/plugins", model.AviSynthPluginFolders);
        Assert.IsType<OpenFolderDialogSettings>(dialogs.LastFrameworkSettings);
    }

    [AvaloniaFact]
    public void SettingsView_BrowseButtons_SitBesidePathFields()
    {
        var model = TestSupport.CreateSettings();
        var view = new SettingsView { DataContext = model };

        using var window = TestSupport.Show(view);
        var library = view.FindControl<TextBox>("VapourSynthLibraryPath")!;
        var browse = view.FindControl<Button>("BrowseVapourSynthLibrary")!;
        var buttons = new[]
        {
            view.FindControl<Button>("BrowseVapourSynthLibrary")!,
            view.FindControl<Button>("BrowseVapourSynthPlugins")!,
            view.FindControl<Button>("BrowseAviSynthLibrary")!,
            view.FindControl<Button>("BrowseAviSynthPlugins")!
        };

        Assert.All(buttons, button => Assert.Equal("...", button.Content));
        Assert.True(browse.Bounds.Left > library.Bounds.Right);
        Assert.True(browse.Bounds.Height >= 28);
    }
}
