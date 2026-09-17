using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Owns the overload insight popup for one editor.
/// </summary>
internal sealed class InsightPresenter(TextEditor editor)
{
    private OverloadInsightWindow? _window;

    /// <summary>
    /// Gets the live insight window, if any.
    /// </summary>
    public OverloadInsightWindow? Window => _window;

    /// <summary>
    /// Shows parameter information, or hides when <paramref name="insight"/> is null.
    /// </summary>
    public void Show(CallInsight? insight)
    {
        Hide();
        if (insight == null)
        {
            return;
        }

        var window = new OverloadInsightWindow(editor.TextArea) { Provider = new OverloadProvider(insight) };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;
        window.Show();
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
