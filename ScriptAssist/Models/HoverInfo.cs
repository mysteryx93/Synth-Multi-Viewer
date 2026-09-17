namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Text shown when the pointer rests on an identifier.
/// </summary>
public sealed record HoverInfo(string Text, int Start, int Length);
