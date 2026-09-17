using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.VapourSynth;

namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Holds the VapourSynth and AviSynth language services and their injected catalogs.
/// </summary>
public class ScriptLanguageFactory : IScriptLanguageFactory
{
    /// <summary>
    /// Host-facing id for VapourSynth. Matches <c>nameof(ScriptKind.VapourSynth)</c> in Synth Multi-Viewer.
    /// </summary>
    public const string VapourSynth = "VapourSynth";

    /// <summary>
    /// Host-facing id for AviSynth. Matches <c>nameof(ScriptKind.AviSynth)</c> in Synth Multi-Viewer.
    /// </summary>
    public const string AviSynth = "AviSynth";

    private readonly Dictionary<string, LanguageProfile> _profiles;

    /// <summary>
    /// Creates the built-in VapourSynth and AviSynth profiles. The host injects native catalogs and optional include readers.
    /// </summary>
    public ScriptLanguageFactory(
        Func<IReadOnlyList<Symbol>> vapoursynthCatalog,
        Func<IReadOnlyList<Symbol>> avisynthCatalog,
        IncludeReader? vapoursynthIncludes = null,
        IncludeReader? avisynthIncludes = null)
        : this(
        [
            new LanguageProfile(
                VapourSynth,
                new VapourSynthLanguage(vapoursynthIncludes),
                new CatalogCache(vapoursynthCatalog.CheckNotNull())),
            new LanguageProfile(
                AviSynth,
                new AviSynthLanguage(avisynthIncludes),
                new CatalogCache(avisynthCatalog.CheckNotNull()))
        ])
    {
    }

    /// <summary>
    /// Creates cached services around the given profiles. Duplicate ids throw <see cref="ArgumentException"/>.
    /// </summary>
    public ScriptLanguageFactory(IReadOnlyList<LanguageProfile> profiles)
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

        foreach (var profile in _profiles.Values)
        {
            if (profile.Service is LanguageService service)
            {
                service.AllowRequests = () => IsEnabled;
            }
        }
    }

    /// <inheritdoc />
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc />
    public ILanguageService? Create(string language)
    {
        language.CheckNotNull();
        if (!IsEnabled)
        {
            return null;
        }

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
            profile.Service.Invalidate();
        }
    }
}
