using Avalonia.Controls;
using Avalonia.Input;
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
        var view = editor.TextArea.TextView;
        if (reply.Hover == null)
        {
            ToolTip.SetTip(view, null);
            ToolTip.SetIsOpen(view, false);
            return;
        }

        ToolTip.SetTip(view, reply.Hover.Text);
        ToolTip.SetIsOpen(view, true);
    }

    /// <summary>
    /// Hides the tooltip.
    /// </summary>
    public void Hide()
    {
        var view = editor.TextArea.TextView;
        ToolTip.SetIsOpen(view, false);
        ToolTip.SetTip(view, null);
    }

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
