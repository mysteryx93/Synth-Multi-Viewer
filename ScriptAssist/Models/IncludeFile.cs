namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Text of an included script and the path it was read from.
/// </summary>
public readonly record struct IncludeFile(string Path, string Text);

