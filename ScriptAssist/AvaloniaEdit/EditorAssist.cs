using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Attaches catalog-driven completion, insight, and hover to an AvaloniaEdit editor.
/// </summary>
public sealed class EditorAssist : IDisposable
{
    private readonly TextEditor _editor;
    private readonly EditorAssistOptions _options;
    private readonly CompletionPresenter _completion;
    private readonly InsightPresenter _insight;
    private readonly HoverPresenter _hover;
    private CancellationTokenSource? _request;
    private CancellationTokenSource? _hoverRequest;
    private long _generation;
    private long _hoverGeneration;
    private bool _listRequest;
    private bool _attached;

    /// <summary>
    /// Creates assistance for <paramref name="editor"/>. Call <see cref="Attach"/> to subscribe.
    /// </summary>
    public EditorAssist(TextEditor editor, EditorAssistOptions options)
    {
        _editor = editor;
        _options = options;
        _completion = new CompletionPresenter(editor);
        _insight = new InsightPresenter(editor);
        _hover = new HoverPresenter(editor);
    }

    /// <summary>
    /// Creates assistance that follows <paramref name="factory"/> enablement, catalogs, and refresh.
    /// </summary>
    public EditorAssist(
        TextEditor editor, IScriptLanguageFactory factory, Func<string> language, Func<string?>? documentPath = null)
        : this(editor, Options(factory, language, documentPath))
    {
    }

    private static EditorAssistOptions Options(
        IScriptLanguageFactory factory, Func<string> language, Func<string?>? documentPath)
    {
        factory.CheckNotNull();
        language.CheckNotNull();
        return new EditorAssistOptions
        {
            ResolveService = () => factory.Create(language()),
            IsEnabled = () => factory.IsEnabled,
            RefreshCatalogs = factory.Refresh,
            ResolveDocumentPath = documentPath
        };
    }

    /// <summary>
    /// Gets the currently displayed result, or null after dismissal.
    /// </summary>
    public Reply? DisplayedReply { get; private set; }

    /// <summary>
    /// Gets the live completion window.
    /// </summary>
    public CompletionWindow? Completion => _completion.Window;

    /// <summary>
    /// Gets the live insight window.
    /// </summary>
    public OverloadInsightWindow? Insight => _insight.Window;

    /// <summary>
    /// Subscribes to editor input. Safe to call more than once.
    /// </summary>
    public void Attach()
    {
        if (_attached) { return; }

        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.Caret.PositionChanged += OnCaretMoved;
        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.TextView.PointerHover += OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        _editor.TextArea.TextView.VisualLinesChanged += OnVisualLinesChanged;
        _attached = true;
    }

    /// <summary>
    /// Unsubscribes and closes popups.
    /// </summary>
    public void Detach()
    {
        if (!_attached) { return; }

        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.Caret.PositionChanged -= OnCaretMoved;
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _editor.TextArea.TextView.PointerHover -= OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
        _editor.TextArea.TextView.VisualLinesChanged -= OnVisualLinesChanged;
        _attached = false;
        Dismiss();
    }

    /// <inheritdoc />
    public void Dispose() => Detach();

    /// <summary>
    /// Cancels pending requests and closes all popups.
    /// </summary>
    public void Dismiss()
    {
        _generation++;
        _hoverGeneration++;
        CancelRequest();
        _hoverRequest?.Cancel();
        _completion.Hide();
        _insight.Hide();
        _hover.Hide();
        DisplayedReply = null;
    }

    /// <summary>
    /// Requests completion for the current snapshot and discards replies after any editor state change.
    /// </summary>
    public async Task RequestAsync(bool showCompletion = true, TimeSpan? delay = null)
    {
        if (_options.IsEnabled?.Invoke() == false)
        {
            Dismiss();
            return;
        }

        _generation++;
        var generation = _generation;
        _listRequest = showCompletion;
        CancelRequest();
        var request = new CancellationTokenSource();
        _request = request;
        try
        {
            await Task.Delay(delay ?? TimeSpan.FromMilliseconds(100), request.Token);
            if (generation != _generation || request.IsCancellationRequested)
            {
                return;
            }

            var service = _options.ResolveService();
            if (service == null)
            {
                return;
            }

            var document = _editor.Document;
            var version = document.Version;
            var caret = _editor.CaretOffset;
            var context = _editor.DataContext;
            var text = _editor.Text;
            if (!_editor.IsKeyboardFocusWithin || !_editor.IsEffectivelyVisible)
            {
                return;
            }

            var reply = await service.GetAsync(text, caret, request.Token, _options.ResolveDocumentPath?.Invoke());
            if (generation != _generation || request.IsCancellationRequested ||
                !ReferenceEquals(document, _editor.Document) || !ReferenceEquals(version, _editor.Document.Version) ||
                caret != _editor.CaretOffset || !ReferenceEquals(context, _editor.DataContext) ||
                !_editor.IsKeyboardFocusWithin || !_editor.IsEffectivelyVisible)
            {
                return;
            }

            DisplayedReply = reply;
            if (showCompletion && reply.Items.Count > 0)
            {
                _completion.Show(reply, text, caret);
                if (CompletingMember(text, caret))
                {
                    _insight.Hide();
                }
                else
                {
                    _insight.Show(reply.Insight);
                }
            }
            else
            {
                _completion.Hide();
                _insight.Show(reply.Insight);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
            }

            if (generation == _generation)
            {
                _listRequest = false;
            }

            request.Dispose();
        }
    }

    private static bool CompletingMember(string text, int caret)
    {
        var i = caret.Clamp(0, text.Length);
        while (i > 0 && BufferLexer.IsIdentifier(text[i - 1]))
        {
            i--;
        }

        return i > 0 && text[i - 1] == '.';
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completion.Window == null || !e.Text.HasValue())
        {
            return;
        }

        var c = e.Text[0];
        if (c is '.' or '(' or '[')
        {
            _completion.Window.CompletionList.RequestInsertion(e);
        }
        else if (!BufferLexer.IsIdentifier(c))
        {
            _completion.Hide();
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (!e.Text.HasValue()) { return; }

        var c = e.Text[^1];
        if (c is '(' or ',' or '[')
        {
            _ = RequestAsync(false, TimeSpan.Zero);
        }
        else if (c == '.')
        {
            _ = RequestAsync(true, TimeSpan.Zero);
        }
        else if (BufferLexer.IsIdentifier(c) && _completion.Window == null)
        {
            _ = RequestAsync(true);
        }
        else if (_insight.Window != null)
        {
            _ = RequestAsync(false, TimeSpan.Zero);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            var hadPopup = _completion.Window != null || _insight.Window != null || _request != null;
            Dismiss();
            e.Handled |= hadPopup;
        }
        else if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = RequestAsync(!e.KeyModifiers.HasFlag(KeyModifiers.Shift), TimeSpan.Zero);
            e.Handled = true;
        }
        else if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _options.RefreshCatalogs?.Invoke();
            _ = RequestAsync(_completion.Window != null, TimeSpan.Zero);
            e.Handled = true;
        }
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        if (_listRequest || _completion.Window != null || _insight.Window == null)
        {
            return;
        }
        _ = RequestAsync(false, TimeSpan.FromMilliseconds(40));
    }

    private async void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (_options.IsEnabled?.Invoke() == false || _completion.Window != null)
        {
            return;
        }

        var service = _options.ResolveService();
        if (service == null)
        {
            return;
        }

        var offset = HoverPresenter.OffsetFromPointer(_editor, e);
        if (offset < 0)
        {
            return;
        }

        _hoverRequest?.Cancel();
        _hoverRequest?.Dispose();
        var request = new CancellationTokenSource();
        _hoverRequest = request;
        _hoverGeneration++;
        var generation = _hoverGeneration;
        var document = _editor.Document;
        var version = document.Version;
        var text = _editor.Text;
        try
        {
            var reply = await service.GetAsync(text, offset, request.Token, _options.ResolveDocumentPath?.Invoke());
            if (generation != _hoverGeneration || request.IsCancellationRequested ||
                !ReferenceEquals(document, _editor.Document) || !ReferenceEquals(version, _editor.Document.Version) ||
                offset != HoverPresenter.OffsetFromPointer(_editor, e))
            {
                return;
            }

            _hover.Show(reply);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (generation == _hoverGeneration)
            {
                _hover.Hide();
            }
        }
        finally
        {
            if (ReferenceEquals(_hoverRequest, request))
            {
                _hoverRequest = null;
            }

            request.Dispose();
        }
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        _hoverGeneration++;
        _hoverRequest?.Cancel();
        _hover.Hide();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        _hoverGeneration++;
        _hover.Hide();
    }

    private void CancelRequest()
    {
        _request?.Cancel();
        _request = null;
    }
}
