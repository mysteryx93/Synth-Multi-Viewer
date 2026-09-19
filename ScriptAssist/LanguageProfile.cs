namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A language implementation paired with its symbol catalog.
/// </summary>
internal sealed class LanguageProfile
{
    /// <summary>
    /// Creates a profile identified by <paramref name="id"/>.
    /// </summary>
    public LanguageProfile(string id, ILanguage language, ISymbolCatalog catalog)
    {
        Id = id.CheckNotNullOrEmpty();
        Catalog = catalog.CheckNotNull();
        Service = new LanguageService(language.CheckNotNull(), catalog);
    }

    /// <summary>
    /// Gets the host-facing identifier used to create and configure this profile.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the catalog cache for this profile.
    /// </summary>
    public ISymbolCatalog Catalog { get; }

    /// <summary>
    /// Gets the configuration key last passed to <see cref="ScriptLanguageFactory.Configure"/>.
    /// </summary>
    public string? CatalogKey { get; set; }

    /// <summary>
    /// Gets the shared language service for this profile.
    /// </summary>
    public ILanguageService Service { get; }
}
