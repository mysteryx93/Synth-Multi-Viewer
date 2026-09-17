namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Creates language services and refreshes injected catalogs.
/// </summary>
public interface IScriptLanguageFactory
{
    /// <summary>
    /// Gets or sets whether assistance is enabled. Defaults to true. Catalog enumeration runs only when this is true.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Returns the shared service for <paramref name="language"/>, or null when the id is unknown.
    /// </summary>
    ILanguageService? Create(string language);

    /// <summary>
    /// Updates the catalog key for <paramref name="language"/>. Enumeration runs only when
    /// <see cref="IsEnabled"/> is true.
    /// </summary>
    void Configure(string language, string catalogKey);

    /// <summary>
    /// Forces every registered catalog to enumerate again.
    /// </summary>
    void Refresh();
}
