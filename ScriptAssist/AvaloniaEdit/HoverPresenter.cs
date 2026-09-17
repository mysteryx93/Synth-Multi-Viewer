using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Shows type and signature text when the pointer rests on an identifier.
/// </summary>
internal sealed class HoverPresenter(TextEditor editor)
{
    /// <summary>
    /// Updates the tooltip from a reply, or hides it when nothing useful is known.
    /// </summary>
    public void Show(Reply reply)
    {
        try
        {
            Hide();
            var text = reply.Hover?.Text;
            if (!text.HasValue())
            {
                return;
            }

            if (!editor.IsEffectivelyVisible)
            {
                return;
            }

            var view = editor.TextArea.TextView;
            ToolTip.SetTip(view, CreateTip(text));
            ToolTip.SetIsOpen(view, true);
        }
        catch
        {
            Hide();
        }
    }

    /// <summary>
    /// Hides the tooltip.
    /// </summary>
    public void Hide()
    {
        try
        {
            var view = editor.TextArea.TextView;
            ToolTip.SetIsOpen(view, false);
            ToolTip.SetTip(view, null);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Caps and wraps hover text the same way completion hints do.
    /// </summary>
    internal static TextBlock CreateTip(string text) =>
        new()
        {
            Text = CompletionData.TruncateHint(text),
            MaxWidth = CompletionData.HintMaxWidth,
            MaxLines = CompletionData.HintMaxLines,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

    /// <summary>
    /// Maps a pointer position to a document offset, or -1 when outside the text.
    /// </summary>
    public static int OffsetFromPointer(TextEditor editor, PointerEventArgs e)
    {
        var view = editor.TextArea.TextView;
        var point = e.GetPosition(view) + view.ScrollOffset;
        var position = view.GetPosition(point);
        if (position == null || editor.Document == null)
        {
            return -1;
        }

        return editor.Document.GetOffset(position.Value.Location);
    }
}
