using System.Reactive.Linq;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Describes an editable script and its optional file path.
/// </summary>
public interface IEditorViewModel : IScriptViewModel
{
    /// <summary>
    /// Gets or sets the script file path, or null for an unsaved script.
    /// </summary>
    string? FileName { get; set; }
    /// <summary>
    /// Gets or sets the script text.
    /// </summary>
    string Script { get; set; }
    /// <summary>
    /// Gets or sets which native engine evaluates the script.
    /// </summary>
    ScriptKind Kind { get; set; }
}

/// <summary>
/// Stores the text and file path of a script editor tab.
/// </summary>
public partial class EditorViewModel : ScriptViewModel, IEditorViewModel
{
    /// <summary>
    /// Creates a closable script editor tab.
    /// </summary>
    public EditorViewModel()
    {
        CanClose = true;
        DisplayName = "Script";
        Sort = 0;
        this.WhenAnyValue(x => x.Kind)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HighlightSource)));
    }

    /// <inheritdoc />
    [Reactive]
    public partial string? FileName { get; set; }

    /// <inheritdoc />
    [Reactive]
    public partial string Script { get; set; } = string.Empty;

    /// <inheritdoc />
    [Reactive]
    public partial ScriptKind Kind { get; set; }

    /// <summary>
    /// Gets the syntax highlighting asset for the current script kind.
    /// </summary>
    public string HighlightSource => Kind == ScriptKind.AviSynth ? "AviSynth.xshd" : "Python.xshd";
}
