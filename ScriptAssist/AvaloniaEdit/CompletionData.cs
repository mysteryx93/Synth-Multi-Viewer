using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Adapts a snapshot completion to AvaloniaEdit's live replacement segment.
/// </summary>
public sealed class CompletionData(CompletionItem item) : ICompletionData
{
    /// <inheritdoc />
    public IImage Image => null!;

    /// <inheritdoc />
    public string Text => item.InsertionText;

    /// <inheritdoc />
    public object Content => Text;

    /// <inheritdoc />
    public object Description
    {
        get
        {
            var text = HintText(item);
            return !text.HasValue() ? null! : new TextBlock
                {
                    Text = text,
                    MaxWidth = HintMaxWidth,
                    MaxLines = HintMaxLines,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
        }
    }

    internal const double HintMaxWidth = 560;
    internal const int HintMaxLines = 8;

    /// <inheritdoc />
    public double Priority => item.Priority;

    /// <inheritdoc />
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, item.InsertionText);

    /// <summary>
    /// Parameter list or property type only; the list already shows the name.
    /// </summary>
    internal static string? HintText(CompletionItem item)
    {
        if (item.Kind is SymbolKind.Property or SymbolKind.Local)
        {
            var colon = item.Signature.IndexOf(':');
            if (colon < 0 || colon + 1 >= item.Signature.Length)
            {
                return null;
            }

            var type = item.Signature[(colon + 1)..].Trim();
            return type.Length == 0 ? null : TruncateHint(type);
        }

        if (item.Kind != SymbolKind.Function)
        {
            return null;
        }

        var signature = item.Signature;
        var open = signature.IndexOf('(');
        var close = signature.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return TruncateHint(signature);
        }

        var inside = signature[(open + 1)..close].Trim();
        return inside.Length == 0 ? "No parameters" : TruncateHint(inside);
    }

    /// <summary>
    /// Caps extreme native signatures; wrapping and line limit handle ordinary length.
    /// </summary>
    internal static string TruncateHint(string text, int max = 400)
    {
        if (!text.HasValue() || text.Length <= max)
        {
            return text;
        }

        return text[..(max - 1)] + "…";
    }
}
