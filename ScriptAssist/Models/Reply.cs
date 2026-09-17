namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Completions, call information, and hover for one immutable editor snapshot.
/// </summary>
public sealed record Reply(
    IReadOnlyList<CompletionItem> Items,
    CallInsight? Insight,
    HoverInfo? Hover = null);
