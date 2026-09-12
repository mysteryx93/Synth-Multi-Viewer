using System.Reactive.Linq;
using HanumanInstitute.MvvmDialogs;

namespace HanumanInstitute.SynthMultiViewer.ViewModels;

/// <summary>
/// Describes a named workspace that can request closure.
/// </summary>
public interface IWorkspaceViewModel
{
    /// <summary>
    /// Occurs when the workspace asks its owner to close it.
    /// </summary>
    event EventHandler? RequestClose;
    /// <summary>
    /// Gets the command that requests closure when allowed.
    /// </summary>
    RxCommandVoid Close { get; }
    /// <summary>
    /// Gets or sets the window title or tab label.
    /// </summary>
    string DisplayName { get; set; }
    /// <summary>
    /// Gets or sets whether the close command is enabled.
    /// </summary>
    bool CanClose { get; set; }
}

/// <summary>
/// Provides a title and close command for windows and tabs.
/// </summary>
public partial class WorkspaceViewModel : ReactiveObject, IWorkspaceViewModel, ICloseable
{
    /// <summary>
    /// Creates an unnamed workspace that can be closed.
    /// </summary>
    public WorkspaceViewModel() { }

    /// <summary>
    /// Creates a workspace with the specified title and close permission.
    /// </summary>
    public WorkspaceViewModel(string displayName, bool canClose)
    {
        DisplayName = displayName;
        CanClose = canClose;
    }

    /// <inheritdoc />
    public event EventHandler? RequestClose;

    /// <inheritdoc />
    [Reactive]
    public partial string DisplayName { get; set; } = string.Empty;

    /// <inheritdoc />
    [Reactive]
    public partial bool CanClose { get; set; } = true;

    /// <inheritdoc />
    public RxCommandVoid Close => field ??= ReactiveCommand.Create(
        CloseView,
        this.WhenAnyValue(x => x.CanClose));

    /// <summary>
    /// Requests closure through the workspace owner or dialog service.
    /// </summary>
    protected void CloseView() => RequestClose?.Invoke(this, EventArgs.Empty);
}
