using Avalonia.Media;
using HanumanInstitute.MvvmDialogs;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Edits a tab hue in a modal color picker.
/// </summary>
public partial class TabColorViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    private Color _color = Colors.White;
    private Color _defaultHue;
    private bool _loading;

    /// <summary>
    /// Creates a tab color picker with an empty result.
    /// </summary>
    public TabColorViewModel() => DisplayName = "Tab color";

    /// <summary>
    /// Gets true after acceptance, or null while unconfirmed.
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets or sets the hue shown in the picker.
    /// </summary>
    public Color Color
    {
        get => _color;
        set
        {
            if (_color == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _color, value);
            if (!_loading)
            {
                UseDefault = false;
            }
        }
    }

    /// <summary>
    /// Gets whether OK should clear the per-tab override.
    /// </summary>
    [Reactive]
    public partial bool UseDefault { get; private set; } = true;

    /// <summary>
    /// Gets the override to apply, or null to restore the engine hue.
    /// </summary>
    public Color? Result => UseDefault ? null : Color.FromRgb(Color.R, Color.G, Color.B);

    /// <summary>
    /// Loads the current tab hue into the picker.
    /// </summary>
    public void Load(Color color, bool useDefault, Color defaultHue)
    {
        DialogResult = null;
        _defaultHue = defaultHue;
        _loading = true;
        Color = color;
        UseDefault = useDefault;
        _loading = false;
    }

    /// <summary>
    /// Restores the engine hue without closing the dialog.
    /// </summary>
    public RxCommandVoid RestoreDefault => field ??= ReactiveCommand.Create(RestoreDefaultImpl);

    /// <summary>
    /// Accepts the selected hue and requests that the dialog close.
    /// </summary>
    public RxCommandVoid Ok => field ??= ReactiveCommand.Create(OkImpl);

    private void RestoreDefaultImpl()
    {
        _loading = true;
        Color = _defaultHue;
        UseDefault = true;
        _loading = false;
    }

    private void OkImpl()
    {
        DialogResult = true;
        CloseView();
    }
}
