namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Provides completions, insight, and hover without evaluating editor text.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Analyzes a snapshot; callers must discard replies after editor state changes.
    /// </summary>
    Task<Reply> GetAsync(string text, int caret, CancellationToken cancellationToken, string? documentPath = null);

    /// <summary>
    /// Drops cached document analysis so the next request rebinds includes and assignments.
    /// </summary>
    void Invalidate();
}
