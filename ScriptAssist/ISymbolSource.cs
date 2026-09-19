namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Produces the native or mapped function catalog for one language.
/// </summary>
public interface ISymbolSource
{
    /// <summary>
    /// Copies the current catalog. May load plugins; the caller runs this off the UI thread.
    /// </summary>
    IReadOnlyList<Symbol> Enumerate();
}
