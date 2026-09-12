namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Describes script output and its playback and scroll positions.
/// </summary>
public interface IViewerViewModel : IScriptViewModel
{
    /// <summary>
    /// Gets or sets the source script file path, or null for an unsaved script.
    /// </summary>
    string? FileName { get; set; }
    /// <summary>
    /// Gets or sets the script to render; null releases the output.
    /// </summary>
    string? Script { get; set; }
    /// <summary>
    /// Gets or sets the zero-based frame index, represented as seconds. The player label shows this as a 1-based number.
    /// </summary>
    TimeSpan Position { get; set; }
    /// <summary>
    /// Gets or sets whether the viewer is playing.
    /// </summary>
    bool IsPlaying { get; set; }
    /// <summary>
    /// Gets or sets the last valid frame index, represented as seconds. The player label shows this plus one as the frame count.
    /// </summary>
    TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the player error, or null when no error is present.
    /// </summary>
    string? ErrorMessage { get; set; }
    /// <summary>
    /// Gets or sets the horizontal scroll offset in display units.
    /// </summary>
    double ScrollHorizontalOffset { get; set; }
    /// <summary>
    /// Gets or sets the vertical scroll offset in display units.
    /// </summary>
    double ScrollVerticalOffset { get; set; }
    /// <summary>
    /// Gets or sets which native engine evaluates the script.
    /// </summary>
    ScriptKind Kind { get; set; }
}

/// <summary>
/// Stores the script and playback state of a viewer tab.
/// </summary>
public partial class ViewerViewModel : ScriptViewModel, IViewerViewModel
{
    /// <summary>
    /// Creates a closable viewer tab.
    /// </summary>
    public ViewerViewModel()
    {
        CanClose = true;
        Sort = 1;
    }

    /// <inheritdoc />
    [Reactive]
    public partial string? FileName { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial string? Script { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial string? ErrorMessage { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial TimeSpan Position { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial bool IsPlaying { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial TimeSpan Duration { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial double ScrollHorizontalOffset { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial double ScrollVerticalOffset { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial ScriptKind Kind { get; set; }
}
