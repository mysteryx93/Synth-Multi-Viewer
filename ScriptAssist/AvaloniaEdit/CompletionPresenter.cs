using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Owns the completion popup for one editor.
/// </summary>
internal sealed class CompletionPresenter(TextEditor editor, AssistTipSize? size = null)
{
    private CompletionWindow? _window;

    /// <summary>
    /// Gets the live completion window, if any.
    /// </summary>
    public CompletionWindow? Window => _window;

    /// <summary>
    /// Shows replacements from a snapshot reply.
    /// </summary>
    public void Show(Reply reply, string text, int caret)
    {
        Hide();
        if (reply.Items.Count == 0)
        {
            return;
        }

        var first = reply.Items[0];
        var window = new CompletionWindow(editor.TextArea)
        {
            StartOffset = first.Start,
            EndOffset = first.Start + first.Length,
            CloseAutomatically = true,
            CompletionList =
            {
                CompletionAcceptAction = CompletionAcceptAction.PointerPressed
            }
        };
        foreach (var item in reply.Items)
        {
            window.CompletionList.CompletionData.Add(new CompletionData(item, size ?? AssistTipSize.Hint));
        }

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;
        window.Show();
        if (first.Start >= 0 && first.Start <= caret && caret <= text.Length)
        {
            window.CompletionList.SelectItem(text[first.Start..caret]);
        }
    }

    /// <summary>
    /// Closes the popup if it is open.
    /// </summary>
    public void Hide()
    {
        _window?.Hide();
        _window = null;
    }
}
