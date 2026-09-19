namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Resolves an import specifier to file text, or null when the specifier is unavailable.
/// </summary>
public interface IIncludeSource
{
    /// <summary>
    /// Follows <paramref name="specifier"/> relative to <paramref name="fromPath"/>.
    /// Return null for a missing or unreadable candidate so lookup can continue.
    /// </summary>
    IncludeFile? Read(string specifier, string? fromPath);
}
