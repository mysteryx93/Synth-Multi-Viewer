namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Text of an included script and the path it was read from.
/// </summary>
public readonly record struct IncludeFile(string Path, string Text);

/// <summary>
/// Reads an include specifier relative to <paramref name="fromPath"/>, or null when missing.
/// </summary>
public delegate IncludeFile? IncludeReader(string specifier, string? fromPath);
