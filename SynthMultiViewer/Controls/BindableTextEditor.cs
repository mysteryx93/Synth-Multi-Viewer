using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// An AvaloniaEdit editor. Bind <see cref="TextEditor.Document"/>; do not two-way bind
/// <see cref="ScriptText"/>, which would recopy the whole buffer on every keystroke.
/// </summary>
public partial class BindableTextEditor : TextEditor
{
    private ScrollViewer? _scroll;

    /// <summary>
    /// Defines the bindable script text.
    /// </summary>
    public static readonly StyledProperty<string> ScriptTextProperty =
        AvaloniaProperty.Register<BindableTextEditor, string>(nameof(ScriptText), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Creates an editor that keeps <see cref="TextEditor.Document"/> as the live buffer.
    /// </summary>
    public BindableTextEditor()
    {
        InitializeCompletion();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scroll != null)
        {
            _scroll.LayoutUpdated -= OnScrollLayoutUpdated;
        }

        base.OnApplyTemplate(e);
        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_scroll != null)
        {
            _scroll.LayoutUpdated += OnScrollLayoutUpdated;
        }
    }

    private void OnScrollLayoutUpdated(object? sender, EventArgs e)
    {
        if (_scroll == null)
        {
            return;
        }

        var viewport = _scroll.Viewport;
        foreach (var bar in _scroll.GetVisualDescendants().OfType<ScrollBar>())
        {
            if (bar.Orientation == Orientation.Horizontal && viewport.Width > 0
                && bar.LargeChange != viewport.Width)
            {
                bar.LargeChange = viewport.Width;
            }
            else if (bar.Orientation == Orientation.Vertical && viewport.Height > 0
                && bar.LargeChange != viewport.Height)
            {
                bar.LargeChange = viewport.Height;
            }
        }
    }

    /// <summary>
    /// Gets or sets the document text.
    /// </summary>
    public string ScriptText
    {
        get => GetValue(ScriptTextProperty);
        set => SetValue(ScriptTextProperty, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(TextEditor);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScriptTextProperty && Text != ScriptText)
        {
            Text = ScriptText ?? string.Empty;
        }
    }
}
