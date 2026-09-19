namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Current editor language, path, and factory for an attached <see cref="EditorAssist"/>.
/// </summary>
public interface IAssistSession
{
    /// <summary>
    /// Resolves the language service for the current editor state.
    /// </summary>
    ILanguageService? ResolveService();

    /// <summary>
    /// Gets whether assistance should run.
    /// </summary>
    bool AssistanceEnabled { get; }

    /// <summary>
    /// Gets the path of the open script, or null when the buffer is unsaved.
    /// </summary>
    string? DocumentPath { get; }

    /// <summary>
    /// Reloads native catalogs.
    /// </summary>
    void RefreshCatalogs();
}
