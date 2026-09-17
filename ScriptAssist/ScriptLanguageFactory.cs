namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Holds one language service per registered profile.
/// </summary>
public sealed class ScriptLanguageFactory : IScriptLanguageFactory
{
    private readonly Dictionary<string, LanguageProfile> _profiles;
    private readonly Func<bool>? _isEnabled;

    /// <summary>
    /// Creates cached services around the given profiles. A null enablement probe means always on.
    /// </summary>
    public ScriptLanguageFactory(IReadOnlyList<LanguageProfile> profiles, Func<bool>? isEnabled = null)
    {
        profiles.CheckNotNull();
        _profiles = new Dictionary<string, LanguageProfile>(StringComparer.Ordinal);
        foreach (var profile in profiles)
        {
            profile.CheckNotNull();
            if (!_profiles.TryAdd(profile.Id, profile))
            {
                throw new ArgumentException("Duplicate language '{0}'.".FormatInvariant(profile.Id), nameof(profiles));
            }
        }

        _isEnabled = isEnabled;
    }

    /// <inheritdoc />
    public bool IsEnabled => _isEnabled?.Invoke() != false;

    /// <inheritdoc />
    public ILanguageService? Create(string language)
    {
        language.CheckNotNull();
        return _profiles.TryGetValue(language, out var profile) ? profile.Service : null;
    }

    /// <inheritdoc />
    public void Configure(string language, string catalogKey)
    {
        language.CheckNotNull();
        catalogKey.CheckNotNull();
        if (!_profiles.TryGetValue(language, out var profile)) { return; }

        if (!IsEnabled)
        {
            profile.Catalog.SetKey(catalogKey);
            return;
        }

        profile.Catalog.Refresh(catalogKey);
    }

    /// <inheritdoc />
    public void Refresh()
    {
        if (!IsEnabled) { return; }

        foreach (var profile in _profiles.Values)
        {
            profile.Catalog.Refresh("explicit", true);
        }
    }
}
