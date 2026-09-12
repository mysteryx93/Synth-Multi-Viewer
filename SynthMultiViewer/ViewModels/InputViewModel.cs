using System.Reactive.Linq;
using HanumanInstitute.MvvmDialogs;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Validates dialog input and requests closure when it is accepted.
/// </summary>
public partial class InputViewModel : WorkspaceViewModel, IModalDialogViewModel
{
    /// <summary>
    /// Creates an input dialog model with an empty value.
    /// </summary>
    public InputViewModel()
    {
        DisplayName = "Input";
        this.WhenAnyValue(x => x.Value)
            .Subscribe(_ => UpdateValid());
    }

    /// <summary>
    /// Gets true after acceptance, or null while unconfirmed.
    /// </summary>
    public bool? DialogResult { get; private set; }

    /// <summary>
    /// Gets or sets the prompt shown above the input box.
    /// </summary>
    [Reactive]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entered text.
    /// </summary>
    [Reactive]
    public partial string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether the current value passes validation.
    /// </summary>
    [Reactive]
    public partial bool IsValid { get; private set; }

    /// <summary>
    /// Gets or sets the validation rule; call Reset after replacing the rule.
    /// </summary>
    public Func<string, bool>? Validate { get; set; }

    /// <summary>
    /// Clears the dialog result and validates the current value.
    /// </summary>
    public void Reset()
    {
        DialogResult = null;
        UpdateValid();
    }

    /// <summary>
    /// Accepts valid input and requests that the dialog close.
    /// </summary>
    public RxCommandVoid Ok => field ??= ReactiveCommand.Create(OkImpl, this.WhenAnyValue(x => x.IsValid));

    private void OkImpl()
    {
        DialogResult = true;
        CloseView();
    }

    private void UpdateValid() => IsValid = Validate == null || Validate.Invoke(Value);
}
