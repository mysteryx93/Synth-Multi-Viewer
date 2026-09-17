namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A cached native or injected function catalog.
/// </summary>
public interface ISymbolCatalog
{
    /// <summary>
    /// Starts background enumeration when the configuration changes or a refresh is forced.
    /// </summary>
    void Refresh(string key, bool force = false);

    /// <summary>
    /// Remembers the configuration key without starting enumeration.
    /// </summary>
    void SetKey(string key);

    /// <summary>
    /// Waits for the shared catalog without cancelling its native enumeration.
    /// </summary>
    Task<IReadOnlyList<Symbol>> GetAsync(CancellationToken token);
}
