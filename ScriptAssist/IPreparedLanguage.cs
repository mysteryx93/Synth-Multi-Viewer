namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Binds from already-masked buffers so analysis does not remask the document.
/// </summary>
internal interface IPreparedLanguage
{
    DocumentBindings Bind(PreparedDocument prepared, IReadOnlyList<Symbol> catalog, CancellationToken token,
        string? documentPath = null);
}
