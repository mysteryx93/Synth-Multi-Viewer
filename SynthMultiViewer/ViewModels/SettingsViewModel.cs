using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using HanumanInstitute.SynthMultiViewer.Models;
using HanumanInstitute.SynthMultiViewer.Services;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Edits a copy of application settings and applies them on OK or Apply.
/// </summary>
public partial class SettingsViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private readonly ISettingsProvider<AppSettingsData> _settingsProvider;
    private readonly IAppTheme _appTheme;
    private readonly IFrameworkDetectionService _frameworks;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Creates a settings dialog with a working copy of the current theme and library paths.
    /// </summary>
    public SettingsViewModel(
        ISettingsProvider<AppSettingsData> settingsProvider, IAppTheme appTheme, IFrameworkDetectionService frameworks,
        IDialogService dialogService)
    {
        _settingsProvider = settingsProvider;
        _appTheme = appTheme;
        _frameworks = frameworks;
        _dialogService = dialogService;
        DisplayName = "Settings";
        Theme = settingsProvider.Value.Theme;
        VapourSynthPath = settingsProvider.Value.VapourSynthPath;
        VapourSynthPluginFolders = settingsProvider.Value.VapourSynthPluginFolders;
        VapourSynthReplacePlugins = settingsProvider.Value.VapourSynthReplacePlugins;
        VapourSynthThreads = EffectiveThreads(settingsProvider.Value.VapourSynthThreads);
        AviSynthPath = settingsProvider.Value.AviSynthPath;
        AviSynthPluginFolders = settingsProvider.Value.AviSynthPluginFolders;
        AviSynthReplacePlugins = settingsProvider.Value.AviSynthReplacePlugins;
        ApplyDetection(_frameworks.VapourSynth, _frameworks.AviSynth);
    }

    /// <inheritdoc />
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets or sets the theme being edited.
    /// </summary>
    [Reactive]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// Gets the list of themes for display.
    /// </summary>
    public IReadOnlyList<AppTheme> ThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    /// <summary>
    /// Gets or sets an optional VapourSynth library file or directory.
    /// </summary>
    [Reactive]
    public partial string VapourSynthPath { get; set; } = "";

    /// <summary>
    /// Gets or sets extra VapourSynth plugin folders, separated by semicolons.
    /// </summary>
    [Reactive]
    public partial string VapourSynthPluginFolders { get; set; } = "";

    /// <summary>
    /// Gets or sets whether auto-detected VapourSynth plugin folders are skipped.
    /// </summary>
    [Reactive]
    public partial bool VapourSynthReplacePlugins { get; set; }

    /// <summary>
    /// Gets or sets the VapourSynth worker count used when multi-threading is enabled.
    /// </summary>
    [Reactive]
    public partial int VapourSynthThreads { get; set; }

    /// <summary>
    /// Gets or sets an optional AviSynth library file or directory.
    /// </summary>
    [Reactive]
    public partial string AviSynthPath { get; set; } = "";

    /// <summary>
    /// Gets or sets extra AviSynth plugin folders, separated by semicolons.
    /// </summary>
    [Reactive]
    public partial string AviSynthPluginFolders { get; set; } = "";

    /// <summary>
    /// Gets or sets whether auto-detected AviSynth plugin folders are skipped.
    /// </summary>
    [Reactive]
    public partial bool AviSynthReplacePlugins { get; set; }

    /// <summary>
    /// Gets whether VapourSynth can be loaded with the current path.
    /// </summary>
    [Reactive]
    public partial bool VapourSynthFound { get; set; }

    /// <summary>
    /// Gets the VapourSynth probe caption shown beside the engine name.
    /// </summary>
    [Reactive]
    public partial string VapourSynthStatus { get; set; } = "(Not Found)";

    /// <summary>
    /// Gets the VapourSynth probe details shown as a tooltip.
    /// </summary>
    [Reactive]
    public partial string? VapourSynthStatusTip { get; set; }

    /// <summary>
    /// Gets whether AviSynth can be loaded with the current path.
    /// </summary>
    [Reactive]
    public partial bool AviSynthFound { get; set; }

    /// <summary>
    /// Gets the AviSynth probe caption shown beside the engine name.
    /// </summary>
    [Reactive]
    public partial string AviSynthStatus { get; set; } = "(Not Found)";

    /// <summary>
    /// Gets the AviSynth probe details shown as a tooltip.
    /// </summary>
    [Reactive]
    public partial string? AviSynthStatusTip { get; set; }

    /// <summary>
    /// Gets the detected VapourSynth library path.
    /// </summary>
    [Reactive]
    public partial string VapourSynthLibraryDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected VapourSynth plugin folders.
    /// </summary>
    [Reactive]
    public partial string VapourSynthPluginsDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected AviSynth library path.
    /// </summary>
    [Reactive]
    public partial string AviSynthLibraryDisplay { get; set; } = "";

    /// <summary>
    /// Gets the detected AviSynth plugin folders.
    /// </summary>
    [Reactive]
    public partial string AviSynthPluginsDisplay { get; set; } = "";

    /// <summary>
    /// Applies changes without closing the window.
    /// </summary>
    public RxCommandVoid Apply => field ??= ReactiveCommand.Create(SaveSettings);

    /// <summary>
    /// Saves changes and closes the window.
    /// </summary>
    public RxCommandVoid Ok => field ??= ReactiveCommand.Create(OkImpl);

    /// <summary>
    /// Closes the window without saving.
    /// </summary>
    public RxCommandVoid Cancel => field ??= ReactiveCommand.Create(CloseView);

    /// <summary>
    /// Restores default settings in the working copy.
    /// </summary>
    public RxCommandVoid RestoreDefault => field ??= ReactiveCommand.Create(RestoreDefaultImpl);

    /// <summary>
    /// Shows a file picker for the VapourSynth library override.
    /// </summary>
    public RxCommandVoid BrowseVapourSynthLibrary =>
        field ??= ReactiveCommand.CreateFromTask(BrowseVapourSynthLibraryAsync);

    /// <summary>
    /// Shows a folder picker and appends a VapourSynth plugin folder.
    /// </summary>
    public RxCommandVoid BrowseVapourSynthPlugins =>
        field ??= ReactiveCommand.CreateFromTask(BrowseVapourSynthPluginsAsync);

    /// <summary>
    /// Shows a file picker for the AviSynth library override.
    /// </summary>
    public RxCommandVoid BrowseAviSynthLibrary =>
        field ??= ReactiveCommand.CreateFromTask(BrowseAviSynthLibraryAsync);

    /// <summary>
    /// Shows a folder picker and appends an AviSynth plugin folder.
    /// </summary>
    public RxCommandVoid BrowseAviSynthPlugins =>
        field ??= ReactiveCommand.CreateFromTask(BrowseAviSynthPluginsAsync);

    private Task BrowseVapourSynthLibraryAsync() =>
        BrowseLibraryAsync(path => VapourSynthPath = path, VapourSynthPath, "Select VapourSynth library");

    private Task BrowseVapourSynthPluginsAsync() =>
        BrowsePluginFolderAsync(folders => VapourSynthPluginFolders = folders, VapourSynthPluginFolders,
            "Select VapourSynth plugin folder");

    private Task BrowseAviSynthLibraryAsync() =>
        BrowseLibraryAsync(path => AviSynthPath = path, AviSynthPath, "Select AviSynth library");

    private Task BrowseAviSynthPluginsAsync() =>
        BrowsePluginFolderAsync(folders => AviSynthPluginFolders = folders, AviSynthPluginFolders,
            "Select AviSynth plugin folder");

    private async Task BrowseLibraryAsync(Action<string> setPath, string current, string title)
    {
        var settings = new OpenFileDialogSettings
        {
            Title = title,
            SuggestedStartLocation = ExistingFolder(current),
            SuggestedFileName = FileNameIfExists(current),
            Filters =
            {
                new FileFilter("Library", LibraryExtensions()),
                new FileFilter("All files", "*")
            }
        };
        var file = await _dialogService.ShowOpenFileDialogAsync(this, settings);
        if (file != null)
        {
            setPath(file.LocalPath);
        }
    }

    private async Task BrowsePluginFolderAsync(Action<string> setFolders, string current, string title)
    {
        var settings = new OpenFolderDialogSettings
        {
            Title = title,
            SuggestedStartLocation = ExistingFolder(FolderListText.Last(current) ?? current)
        };
        var folder = await _dialogService.ShowOpenFolderDialogAsync(this, settings);
        if (folder != null)
        {
            setFolders(FolderListText.Append(current, folder.LocalPath));
        }
    }

    private static IReadOnlyList<string> LibraryExtensions() =>
        OperatingSystem.IsWindows() ? ["dll"] : OperatingSystem.IsMacOS() ? ["dylib"] : ["so"];

    private static string FileNameIfExists(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFileName(path) : "";

    private static IDialogStorageFolder? ExistingFolder(string? path)
    {
        var folder = FolderPath(path);
        return folder != null && Directory.Exists(folder) ? new DesktopDialogStorageFolder(folder) : null;
    }

    private static string? FolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void OkImpl()
    {
        SaveSettings();
        DialogResult = true;
        CloseView();
    }

    private void SaveSettings()
    {
        _settingsProvider.Value.Theme = Theme;
        _settingsProvider.Value.VapourSynthPath = VapourSynthPath.Trim();
        _settingsProvider.Value.VapourSynthPluginFolders = VapourSynthPluginFolders.Trim();
        _settingsProvider.Value.VapourSynthReplacePlugins = VapourSynthReplacePlugins;
        _settingsProvider.Value.VapourSynthThreads = Math.Max(1, VapourSynthThreads);
        _settingsProvider.Value.AviSynthPath = AviSynthPath.Trim();
        _settingsProvider.Value.AviSynthPluginFolders = AviSynthPluginFolders.Trim();
        _settingsProvider.Value.AviSynthReplacePlugins = AviSynthReplacePlugins;
        _appTheme.RequestedTheme = Theme.ToString();
        _frameworks.Apply(_settingsProvider.Value);
        ApplyDetection(_frameworks.VapourSynth, _frameworks.AviSynth);
        _settingsProvider.Save();
    }

    private void RestoreDefaultImpl()
    {
        Theme = AppTheme.Light;
        VapourSynthPath = "";
        VapourSynthPluginFolders = "";
        VapourSynthReplacePlugins = false;
        VapourSynthThreads = Environment.ProcessorCount;
        AviSynthPath = "";
        AviSynthPluginFolders = "";
        AviSynthReplacePlugins = false;
    }

    private void ApplyDetection(FrameworkInstall vapourSynth, FrameworkInstall aviSynth)
    {
        VapourSynthFound = vapourSynth.Found;
        AviSynthFound = aviSynth.Found;
        VapourSynthStatus = FormatStatus(vapourSynth);
        VapourSynthStatusTip = FormatStatusTip(vapourSynth);
        AviSynthStatus = FormatStatus(aviSynth);
        AviSynthStatusTip = FormatStatusTip(aviSynth);
        VapourSynthLibraryDisplay = vapourSynth.LibraryPath ?? "";
        VapourSynthPluginsDisplay = FormatPlugins(vapourSynth);
        AviSynthLibraryDisplay = aviSynth.LibraryPath ?? "";
        AviSynthPluginsDisplay = FormatPlugins(aviSynth);
    }

    private static string FormatStatus(FrameworkInstall install) => install.Status switch
    {
        FrameworkStatus.Detected when !string.IsNullOrWhiteSpace(install.Version) =>
            "(Detected " + install.Version + ")",
        FrameworkStatus.Detected => "(Detected)",
        FrameworkStatus.Error => "(Error)",
        _ => "(Not Found)"
    };

    private static string? FormatStatusTip(FrameworkInstall install)
    {
        if (!string.IsNullOrWhiteSpace(install.Message))
        {
            return install.Message;
        }

        if (!string.IsNullOrWhiteSpace(install.VersionDetail))
        {
            return install.VersionDetail;
        }

        return install.Status == FrameworkStatus.Error
            ? "The library could not be loaded or could not run a script."
            : null;
    }

    private static string FormatPlugins(FrameworkInstall install) =>
        install.PluginDirectories is { Count: > 0 } directories ? string.Join("; ", directories) : "";

    private static int EffectiveThreads(int threads) => threads > 0 ? threads : Environment.ProcessorCount;
}
