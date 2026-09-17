namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// An insertion and its UTF-16 replacement range in the requested document snapshot.
/// </summary>
public sealed record CompletionItem(
    string InsertionText,
    int Start,
    int Length,
    SymbolKind Kind,
    string Signature,
    double Priority = 0);
