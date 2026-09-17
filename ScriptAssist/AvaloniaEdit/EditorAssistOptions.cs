namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Host callbacks used to attach assistance to an AvaloniaEdit editor.
/// </summary>
public sealed class EditorAssistOptions
{
    /// <summary>
    /// Resolves the language service for the current editor state.
    /// </summary>
    public required Func<ILanguageService?> ResolveService { get; init; }

    /// <summary>
    /// When false, assistance is a no-op and catalogs are not requested.
    /// </summary>
    public Func<bool>? IsEnabled { get; init; }

    /// <summary>
    /// Explicitly refreshes native catalogs, if the host supports it.
    /// </summary>
    public Action? RefreshCatalogs { get; init; }

    /// <summary>
    /// Gets the path of the open script, or null when the buffer is unsaved.
    /// </summary>
    public Func<string?>? ResolveDocumentPath { get; init; }
}
