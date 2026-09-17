using Avalonia;
using AvaloniaEdit.CodeCompletion;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Splat;

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
    /// Gets or sets an optional language service supplied by tests or a host.
    /// </summary>
    public ILanguageService? LanguageService { get; set; }

    /// <summary>
    /// Gets or sets an optional factory; the locator is used when this is null.
    /// </summary>
    public IScriptLanguageFactory? LanguageFactory { get; set; }

    /// <summary>
    /// Gets the currently displayed result, or null after dismissal.
    /// </summary>
    public Reply? DisplayedReply => _assist?.DisplayedReply;

    internal CompletionWindow? Completion => _assist?.Completion;

    internal OverloadInsightWindow? Insight => _assist?.Insight;

    private EditorAssist? _assist;

    private void InitializeCompletion()
    {
        _assist = new EditorAssist(this, new EditorAssistOptions
        {
            ResolveService = ResolveService,
            IsEnabled = () => LanguageService != null || ResolveFactory()?.IsEnabled != false,
            RefreshCatalogs = () => ResolveFactory()?.Refresh(),
            ResolveDocumentPath = () => (DataContext as IEditorViewModel)?.FileName
        });
        _assist.Attach();
        DetachedFromVisualTree += (_, _) => _assist.Dismiss();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ScriptKindProperty || e.Property == DocumentProperty)
            {
                _assist.Dismiss();
            }
        };
    }

    private IScriptLanguageFactory? ResolveFactory() =>
        LanguageFactory ?? Locator.Current.GetService<IScriptLanguageFactory>();

    private ILanguageService? ResolveService()
    {
        if (LanguageService != null)
        {
            return LanguageService;
        }

        var factory = ResolveFactory();
        if (factory == null || !factory.IsEnabled)
        {
            return null;
        }

        return factory.Create(ScriptKind.ToString());
    }

    /// <summary>
    /// Cancels pending requests and closes all editor assistance popups.
    /// </summary>
    public void DismissCompletion() => _assist?.Dismiss();

    /// <summary>
    /// Requests completion for the current snapshot and discards replies after any editor state change.
    /// </summary>
    public Task RequestCompletionAsync(bool showCompletion = true, TimeSpan? delay = null) =>
        _assist?.RequestAsync(showCompletion, delay) ?? Task.CompletedTask;
}
