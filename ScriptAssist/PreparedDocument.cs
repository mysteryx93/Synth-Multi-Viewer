namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Comment-masked document text shared between snapshot analysis and language binders.
/// </summary>
internal sealed record PreparedDocument(LexedBuffer Masked, LexedBuffer Quoted);
