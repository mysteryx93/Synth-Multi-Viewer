using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Shows type and signature text when the pointer rests on an identifier.
/// </summary>
internal sealed class HoverPresenter(TextEditor editor, AssistTipSize? size = null)
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
            ToolTip.SetTip(view, CreatePopup(text, size));
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
    /// Caps and wraps hover text using <see cref="AssistTipSize.Hover"/> unless a size is supplied.
    /// </summary>
    internal static TextBlock CreateTip(string text, AssistTipSize? size = null) =>
        CompletionData.HintBlock(text, size ?? AssistTipSize.Hover);

    /// <summary>
    /// Fluent sets <c>ToolTipContentMaxWidth</c> to 320 on the tooltip chrome. Putting the
    /// block in a <see cref="ToolTip"/> with a local MaxWidth keeps hover at <paramref name="size"/>.
    /// </summary>
    internal static ToolTip CreatePopup(string text, AssistTipSize? size = null)
    {
        size ??= AssistTipSize.Hover;
        return new ToolTip
        {
            Content = CreateTip(text, size),
            MaxWidth = Math.Max(1, size.MaxWidth)
        };
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
