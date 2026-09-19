using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Adapts a snapshot completion to AvaloniaEdit's live replacement segment.
/// </summary>
public sealed class CompletionData : ICompletionData
{
    private readonly CompletionItem _item;
    private readonly AssistTipSize _size;

    /// <summary>
    /// Creates completion data using <see cref="AssistTipSize.Hint"/>.
    /// </summary>
    public CompletionData(CompletionItem item) : this(item, AssistTipSize.Hint)
    {
    }

    /// <summary>
    /// Creates completion data that wraps the side-panel hint to <paramref name="size"/>.
    /// </summary>
    public CompletionData(CompletionItem item, AssistTipSize size)
    {
        _item = item;
        _size = size;
    }

    /// <inheritdoc />
    public IImage Image => null!;

    /// <inheritdoc />
    public string Text => _item.InsertionText;

    /// <inheritdoc />
    public object Content => Text;

    /// <inheritdoc />
    public object Description
    {
        get
        {
            var text = HintText(_item, _size);
            return !text.HasValue() ? null! : HintBlock(text, _size);
        }
    }

    /// <inheritdoc />
    public double Priority => _item.Priority;

    /// <inheritdoc />
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, _item.InsertionText);

    /// <summary>
    /// Function signature, or property/local type. The list already shows the name.
    /// </summary>
    internal static string? HintText(CompletionItem item, AssistTipSize? size = null)
    {
        size ??= AssistTipSize.Hint;
        if (item.Kind is SymbolKind.Property or SymbolKind.Local)
        {
            var colon = item.Signature.IndexOf(':');
            if (colon < 0 || colon + 1 >= item.Signature.Length)
            {
                return null;
            }

            var type = item.Signature[(colon + 1)..].Trim();
            return type.Length == 0 ? null : TruncateHint(type, size.MaxCharacters);
        }

        return item.Kind == SymbolKind.Function ? TruncateHint(item.Signature, size.MaxCharacters) : null;
    }

    /// <summary>
    /// Wraps hint or hover text to <paramref name="size"/>. The box sizes to the text
    /// so short signatures are not padded to a fixed width.
    /// </summary>
    internal static TextBlock HintBlock(string text, AssistTipSize? size = null)
    {
        size ??= AssistTipSize.Hint;
        var maxChars = Math.Max(1, size.MaxCharacters);
        return new TextBlock
        {
            Text = TruncateHint(text, maxChars),
            MaxWidth = Math.Max(1, size.MaxWidth),
            MaxLines = Math.Max(1, size.MaxLines),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    /// <summary>
    /// Caps extreme native signatures; wrapping and line limit handle ordinary length.
    /// </summary>
    internal static string TruncateHint(string text, int max)
    {
        if (!text.HasValue() || text.Length <= max)
        {
            return text;
        }

        return text[..(max - 1)] + "…";
    }
}
