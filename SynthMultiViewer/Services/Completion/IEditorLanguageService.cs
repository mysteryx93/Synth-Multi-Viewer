namespace HanumanInstitute.SynthMultiViewer.Services.Completion;

/// <summary>
/// Describes the source of a completion item.
/// </summary>
public enum CompletionKind
{
    /// <summary>
    /// A static language keyword.
    /// </summary>
    Keyword,
    /// <summary>
    /// An assigned buffer variable.
    /// </summary>
    Local,
    /// <summary>
    /// A plugin namespace or module member.
    /// </summary>
    Namespace,
    /// <summary>
    /// A native filter or buffer function.
    /// </summary>
    Function
}

/// <summary>
/// A managed function signature or named language symbol.
/// </summary>
public sealed record FilterSymbol(string Name, string[]? Parameters, CompletionKind Kind = CompletionKind.Function, bool ImplicitLast = false)
{
    /// <summary>
    /// Gets the display signature, retaining unknown native parameter information.
    /// </summary>
    public string Signature => Kind == CompletionKind.Function
        ? Name + "(" + (Parameters == null ? "parameters unknown" : string.Join(", ", Parameters)) + ")" + (ImplicitLast ? " [implicit last]" : "") : Name;
}

/// <summary>
/// An insertion and its UTF-16 replacement range in the requested document snapshot.
/// </summary>
public sealed record EditorCompletion(string InsertionText, int Start, int Length, CompletionKind Kind, string Signature);

/// <summary>
/// Overloads for the enclosing call and its zero-based active argument.
/// </summary>
public sealed record CallInsight(IReadOnlyList<FilterSymbol> Overloads, int ActiveParameter, bool ImplicitClip);

/// <summary>
/// Completions and call information for one immutable editor snapshot.
/// </summary>
public sealed record EditorReply(IReadOnlyList<EditorCompletion> Items, CallInsight? Insight);

/// <summary>
/// Provides filter completion without evaluating editor text.
/// </summary>
public interface IEditorLanguageService
{
    /// <summary>
    /// Analyzes a snapshot; callers must discard replies after editor state changes.
    /// </summary>
    Task<EditorReply> GetAsync(string text, int caret, CancellationToken cancellationToken);
}

