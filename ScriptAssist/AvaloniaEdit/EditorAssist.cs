using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Attaches catalog-driven completion, insight, and hover to an AvaloniaEdit editor.
/// </summary>
public sealed class EditorAssist : IDisposable
{
    private readonly TextEditor _editor;
    private readonly IAssistSession _session;
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
    private TextDocument? _document;
    private readonly Lock _textGate = new();
    private TextDocument? _textDocument;
    private object? _textVersion;
    private string? _text;

    /// <summary>
    /// Creates assistance for <paramref name="editor"/>. Call <see cref="Attach"/> to subscribe.
    /// </summary>
    public EditorAssist(TextEditor editor, IAssistSession session, EditorAssistOptions? options = null)
    {
        _editor = editor;
        _session = session.CheckNotNull();
        _options = options ?? new EditorAssistOptions();
        _completion = new(editor, _options.Hint);
        _insight = new(editor, _options.Hover);
        _hover = new(editor, _options.Hover);
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
        _editor.PropertyChanged += OnEditorPropertyChanged;
        BindDocument();
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
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        BindDocument(null);
        _attached = false;
        Dismiss();
        ClearTextCache();
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
        if (!_session.AssistanceEnabled)
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

            if (Disabled())
            {
                Dismiss();
                return;
            }

            var service = _session.ResolveService();
            if (service == null)
            {
                Dismiss();
                return;
            }

            var document = _editor.Document;
            var version = document.Version;
            var caret = _editor.CaretOffset;
            var context = _editor.DataContext;
            var snapshot = document.CreateSnapshot();
            var path = _session.DocumentPath;
            if (!_editor.IsKeyboardFocusWithin || !_editor.IsEffectivelyVisible)
            {
                return;
            }

            var text = await MaterializeAsync(document, snapshot, version, request.Token);
            var reply = await QueryAsync(service, text, caret, request.Token, path, showCompletion);
            if (generation != _generation || request.IsCancellationRequested ||
                !ReferenceEquals(document, _editor.Document) || !ReferenceEquals(version, _editor.Document.Version) ||
                caret != _editor.CaretOffset || !ReferenceEquals(context, _editor.DataContext) ||
                !_editor.IsKeyboardFocusWithin || !_editor.IsEffectivelyVisible ||
                CallbacksChanged(service, path))
            {
                return;
            }

            if (Disabled())
            {
                Dismiss();
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
            _session.RefreshCatalogs();
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
        if (Disabled() || _completion.Window != null || !_editor.IsEffectivelyVisible)
        {
            return;
        }

        var offset = HoverPresenter.OffsetFromPointer(_editor, e);
        if (offset < 0)
        {
            return;
        }

        await RequestHoverAsync(offset, e);
    }

    internal async Task RequestHoverAsync(int offset, PointerEventArgs? pointer = null)
    {
        if (Disabled() || _completion.Window != null || !_editor.IsEffectivelyVisible)
        {
            return;
        }

        var service = _session.ResolveService();
        if (service == null)
        {
            Dismiss();
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
        var snapshot = document.CreateSnapshot();
        var path = _session.DocumentPath;
        try
        {
            var text = await MaterializeAsync(document, snapshot, version, request.Token);
            var reply = await QueryAsync(service, text, offset, request.Token, path, false);
            if (generation != _hoverGeneration || request.IsCancellationRequested ||
                !ReferenceEquals(document, _editor.Document) || !ReferenceEquals(version, _editor.Document.Version) ||
                (pointer != null && offset != HoverPresenter.OffsetFromPointer(_editor, pointer)) ||
                CallbacksChanged(service, path))
            {
                return;
            }

            if (Disabled())
            {
                _hover.Hide();
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

    private void OnEditorPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextEditor.DocumentProperty)
        {
            BindDocument();
            ClearTextCache();
            Dismiss();
        }
    }

    private void BindDocument(TextDocument? document)
    {
        if (_document != null)
        {
            _document.Changed -= OnDocumentChanged;
        }

        _document = document;
        if (_document != null)
        {
            _document.Changed += OnDocumentChanged;
        }
    }

    private void BindDocument() => BindDocument(_editor.Document);

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        var pending = _request != null || _completion.Window != null || _insight.Window != null;
        var showCompletion = _listRequest || _completion.Window != null;
        CancelAnalysis();
        if (pending)
        {
            _ = RequestAsync(showCompletion);
        }
    }

    private void CancelAnalysis()
    {
        _generation++;
        _hoverGeneration++;
        CancelRequest();
        _hoverRequest?.Cancel();
        _hover.Hide();
    }

    private Task<string> MaterializeAsync(TextDocument document, ITextSource snapshot, object version,
        CancellationToken token)
    {
        lock (_textGate)
        {
            if (ReferenceEquals(_textDocument, document) && ReferenceEquals(_textVersion, version) &&
                _text != null)
            {
                return Task.FromResult(_text);
            }
        }

        return Task.Run(() =>
        {
            lock (_textGate)
            {
                if (ReferenceEquals(_textDocument, document) && ReferenceEquals(_textVersion, version) &&
                    _text != null)
                {
                    return _text;
                }
            }

            var text = snapshot.Text;
            token.ThrowIfCancellationRequested();
            lock (_textGate)
            {
                if (token.IsCancellationRequested)
                {
                    return text;
                }

                _textDocument = document;
                _textVersion = version;
                _text = text;
            }

            return text;
        }, token);
    }

    private void ClearTextCache()
    {
        lock (_textGate)
        {
            _textDocument = null;
            _textVersion = null;
            _text = null;
        }
    }

    private static Task<Reply> QueryAsync(ILanguageService service, string text, int caret,
        CancellationToken token, string? path, bool completions)
    {
        if (service is LanguageService language)
        {
            return language.GetAsync(text, caret, token, path, completions);
        }

        return service.GetAsync(text, caret, token, path);
    }

    private void CancelRequest()
    {
        _request?.Cancel();
        _request = null;
    }

    private bool Disabled() => !_session.AssistanceEnabled;

    private bool CallbacksChanged(ILanguageService? service, string? path) =>
        !ReferenceEquals(service, _session.ResolveService()) ||
        !string.Equals(path, _session.DocumentPath, StringComparison.Ordinal);
}
