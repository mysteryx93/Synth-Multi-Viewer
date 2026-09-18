namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Optional hook so a shared language instance can drop include state on refresh.
/// </summary>
internal interface IRefreshableLanguage
{
    /// <summary>
    /// Drops remembered includes and other bind-lifetime caches.
    /// </summary>
    void Invalidate();
}
