using System.Globalization;
using System.Reactive.Disposables;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.SynthMultiViewer.Helpers;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// A row in the video properties window.
/// </summary>
public sealed class PropertyItem
{
    /// <summary>
    /// Creates a property row or section header.
    /// </summary>
    public PropertyItem(string name, string value, bool isHeader = false)
    {
        Name = name;
        Value = value;
        IsHeader = isHeader;
    }

    /// <summary>
    /// Gets the left-column label, or the section title when <see cref="IsHeader"/> is true.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the right-column value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets whether this row is a section header.
    /// </summary>
    public bool IsHeader { get; }
}

/// <summary>
/// Displays clip and frame properties for the active viewer.
/// </summary>
public partial class VideoPropertiesViewModel : WorkspaceViewModel, IViewClosed
{
    private readonly SerialDisposable _viewer = new();

    /// <summary>
    /// Creates an empty properties window.
    /// </summary>
    public VideoPropertiesViewModel()
    {
        DisplayName = "Video Properties";
        this.WhenAnyValue(x => x.Viewer).Subscribe(OnViewerChanged);
    }

    /// <summary>
    /// Gets the rows shown in the properties window.
    /// </summary>
    public ObservableCollection<PropertyItem> Items { get; } = [];

    /// <summary>
    /// Gets or sets the viewer whose clip and frame are displayed.
    /// </summary>
    [Reactive]
    public partial IViewerViewModel? Viewer { get; set; }

    /// <summary>
    /// Gets or sets the session placement bound by the properties window.
    /// </summary>
    public VideoPropertiesPlacement Placement { get; set; } = new();

    /// <summary>
    /// Clears the window so it can be opened again after the view is closed.
    /// </summary>
    public void OnClosed() => CloseView();

    private void OnViewerChanged(IViewerViewModel? viewer)
    {
        if (viewer == null)
        {
            _viewer.Disposable = null;
            Rebuild(null);
            return;
        }

        _viewer.Disposable = viewer.WhenAnyValue(
                x => x.ClipInfo,
                x => x.FrameProperties,
                x => x.Position,
                x => x.ErrorMessage)
            .Subscribe(_ => Rebuild(viewer));
    }

    /// <summary>
    /// Rebuilds the displayed rows from the current viewer.
    /// </summary>
    public void Rebuild(IViewerViewModel? viewer)
    {
        Items.Clear();
        if (viewer?.ClipInfo is not { } clip || viewer.ErrorMessage != null)
        {
            Items.Add(new("No clip loaded", "", true));
            return;
        }

        Items.Add(new("Clip", "", true));
        Add("Host", clip.Host == ScriptKind.AviSynth ? "AviSynth" : "VapourSynth");
        Add("Size", clip.Width + "×" + clip.Height);
        Add("Frames", clip.FrameCount.ToString(CultureInfo.InvariantCulture));
        Add("Frame rate", FormatFrameRate(clip.FpsNumerator, clip.FpsDenominator));
        Add("Duration", FormatDuration(clip.FrameCount, clip.FpsNumerator, clip.FpsDenominator));
        Add("Format", clip.FormatName);
        Add("Color family", clip.ColorFamily);
        Add("Bit depth", clip.BitDepth.ToString(CultureInfo.InvariantCulture));
        Add("Sample type", clip.SampleType);
        if (clip.Subsampling != null)
        {
            Add("Subsampling", clip.Subsampling);
        }

        Add("Planes", clip.Planes.ToString(CultureInfo.InvariantCulture));

        Items.Add(new("Frame", "", true));
        Add("Index", ((int)viewer.Position.TotalSeconds + 1).ToString(CultureInfo.InvariantCulture));
        if (viewer.FrameProperties.Count == 0)
        {
            Add("Properties", "None");
            return;
        }

        foreach (var property in viewer.FrameProperties)
        {
            Add(property.Name, FramePropertyDisplay.Format(property.Name, property.Value));
        }
    }

    private void Add(string name, string value) => Items.Add(new(name, value));

    /// <summary>
    /// Formats a rational frame rate for display.
    /// </summary>
    public static string FormatFrameRate(long numerator, long denominator)
    {
        if (numerator <= 0 || denominator <= 0)
        {
            return "—";
        }

        var fps = (double)numerator / denominator;
        if (denominator == 1)
        {
            return fps.ToString("0.###", CultureInfo.InvariantCulture) + " fps";
        }

        return numerator + "/" + denominator + " (" + fps.ToString("0.###", CultureInfo.InvariantCulture) + " fps)";
    }

    /// <summary>
    /// Formats clip duration from frame count and frame rate.
    /// </summary>
    public static string FormatDuration(int frames, long numerator, long denominator)
    {
        if (frames <= 0 || numerator <= 0 || denominator <= 0)
        {
            return "—";
        }

        var seconds = frames * (double)denominator / numerator;
        var time = TimeSpan.FromSeconds(seconds);
        return time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss\.fff" : @"mm\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
