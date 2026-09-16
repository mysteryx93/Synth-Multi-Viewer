using Avalonia;
using Avalonia.Data;
using AvaloniaEdit;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// An AvaloniaEdit editor whose text supports two-way bindings.
/// </summary>
public partial class BindableTextEditor : TextEditor
{
    /// <summary>
    /// Defines the bindable script text.
    /// </summary>
    public static readonly StyledProperty<string> ScriptTextProperty =
        AvaloniaProperty.Register<BindableTextEditor, string>(nameof(ScriptText), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Creates an editor that publishes document edits to its text binding.
    /// </summary>
    public BindableTextEditor()
    {
        TextChanged += (_, _) => SetCurrentValue(ScriptTextProperty, Text);
        InitializeCompletion();
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
