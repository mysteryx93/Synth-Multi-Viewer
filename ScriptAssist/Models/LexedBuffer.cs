namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Position-preserving code and whether the buffer ends inside a comment or string.
/// </summary>
internal sealed record LexedBuffer(string Code, bool InLiteral);
