using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit.CodeCompletion;
using HanumanInstitute.SynthMultiViewer.Services.Completion;

namespace HanumanInstitute.SynthMultiViewer.Controls;

public partial class BindableTextEditor
{
    /// <summary>
    /// Defines the script language used for completion.
    /// </summary>
    public static readonly StyledProperty<ScriptKind> ScriptKindProperty =
        AvaloniaProperty.Register<BindableTextEditor, ScriptKind>(nameof(ScriptKind));

    /// <summary>
    /// Gets or sets the script language.
    /// </summary>
    public ScriptKind ScriptKind
    {
        get => GetValue(ScriptKindProperty);
        set => SetValue(ScriptKindProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional language service supplied by the host.
    /// </summary>
    public IEditorLanguageService? LanguageService { get; set; }

    /// <summary>
    /// Gets the currently displayed result, or null after dismissal.
    /// </summary>
    public EditorReply? DisplayedReply { get; private set; }

    internal CompletionWindow? Completion => _completion;

    internal OverloadInsightWindow? Insight => _insight;

    private CompletionWindow? _completion;
    private OverloadInsightWindow? _insight;
    private CancellationTokenSource? _request;
    private EditorLanguageService? _catalogService;
    private long _generation;
    private bool _listRequest;

    private void InitializeCompletion()
    {
        TextArea.TextEntering += OnTextEntering;
        TextArea.TextEntered += OnTextEntered;
        TextArea.Caret.PositionChanged += OnCaretMoved;
        AddHandler(KeyDownEvent, OnCompletionKeyDown, RoutingStrategies.Tunnel);
        DetachedFromVisualTree += (_, _) => DismissCompletion();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ScriptKindProperty)
            {
                _catalogService = null;
                DismissCompletion();
            }
            else if (e.Property == DocumentProperty)
            {
                DismissCompletion();
            }
        };
    }

    private IEditorLanguageService ResolveService() =>
        LanguageService ?? (_catalogService ??= new EditorLanguageService(
            ScriptKind == ScriptKind.AviSynth,
            ScriptKind == ScriptKind.AviSynth ? EditorCatalogs.AviSynth : EditorCatalogs.VapourSynth));

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completion == null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var c = e.Text[0];
        if (c is '.' or '(' or '[')
        {
            _completion.CompletionList.RequestInsertion(e);
        }
        else if (!BufferLexer.IsIdentifier(c))
        {
            HideCompletion();
        }
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var c = e.Text[^1];
        if (c is '(' or ',')
        {
            _ = RequestCompletionAsync(false, TimeSpan.Zero);
        }
        else if (c == '.')
        {
            _ = RequestCompletionAsync(true, TimeSpan.Zero);
        }
        else if (BufferLexer.IsIdentifier(c) && _completion == null)
        {
            _ = RequestCompletionAsync(true);
        }
        else if (_insight != null)
        {
            _ = RequestCompletionAsync(false, TimeSpan.Zero);
        }
    }

    private void OnCompletionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            var hadPopup = _completion != null || _insight != null || _request != null;
            DismissCompletion();
            e.Handled |= hadPopup;
        }
        else if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _ = RequestCompletionAsync(!e.KeyModifiers.HasFlag(KeyModifiers.Shift), TimeSpan.Zero);
            e.Handled = true;
        }
        else if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            EditorCatalogs.Refresh();
            _ = RequestCompletionAsync(_completion != null, TimeSpan.Zero);
            e.Handled = true;
        }
    }

    private void OnCaretMoved(object? sender, EventArgs e)
    {
        if (_listRequest || _completion != null || _insight == null)
        {
            return;
        }

        _ = RequestCompletionAsync(false, TimeSpan.FromMilliseconds(40));
    }

    /// <summary>
    /// Cancels pending requests and closes all editor assistance popups.
    /// </summary>
    public void DismissCompletion()
    {
        _generation++;
        CancelRequest();
        HideCompletion();
        HideInsight();
        DisplayedReply = null;
    }

    /// <summary>
    /// Requests completion for the current snapshot and discards replies after any editor state change.
    /// </summary>
    public async Task RequestCompletionAsync(bool showCompletion = true, TimeSpan? delay = null)
    {
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

            var document = Document;
            var version = document.Version;
            var caret = CaretOffset;
            var context = DataContext;
            var kind = ScriptKind;
            var text = Text;
            if (!IsKeyboardFocusWithin || !IsEffectivelyVisible)
            {
                return;
            }

            var reply = await ResolveService().GetAsync(text, caret, request.Token);
            if (generation != _generation || request.IsCancellationRequested ||
                !ReferenceEquals(document, Document) || !ReferenceEquals(version, Document.Version) ||
                caret != CaretOffset || !ReferenceEquals(context, DataContext) || kind != ScriptKind ||
                !IsKeyboardFocusWithin || !IsEffectivelyVisible)
            {
                return;
            }

            DisplayedReply = reply;
            if (showCompletion && reply.Items.Count > 0)
            {
                ShowCompletion(reply, text, caret);
                HideInsight();
            }
            else
            {
                HideCompletion();
                ShowInsight(reply.Insight);
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

    private void ShowCompletion(EditorReply reply, string text, int caret)
    {
        HideCompletion();
        if (reply.Items.Count == 0)
        {
            return;
        }

        var first = reply.Items[0];
        var window = new CompletionWindow(TextArea)
        {
            StartOffset = first.Start,
            EndOffset = first.Start + first.Length,
            CloseAutomatically = true
        };
        window.CompletionList.CompletionAcceptAction = CompletionAcceptAction.PointerPressed;
        foreach (var item in reply.Items)
        {
            window.CompletionList.CompletionData.Add(new EditorCompletionData(item));
        }

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_completion, window))
            {
                _completion = null;
            }
        };
        _completion = window;
        window.Show();
        if (first.Start >= 0 && first.Start <= caret && caret <= text.Length)
        {
            window.CompletionList.SelectItem(text[first.Start..caret]);
        }
    }

    private void ShowInsight(CallInsight? insight)
    {
        HideInsight();
        if (insight == null)
        {
            return;
        }

        var window = new OverloadInsightWindow(TextArea) { Provider = new EditorOverloadProvider(insight) };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_insight, window))
            {
                _insight = null;
            }
        };
        _insight = window;
        window.Show();
    }

    private void HideCompletion()
    {
        _completion?.Hide();
        _completion = null;
    }

    private void HideInsight()
    {
        _insight?.Hide();
        _insight = null;
    }

    private void CancelRequest()
    {
        _request?.Cancel();
        _request = null;
    }
}
