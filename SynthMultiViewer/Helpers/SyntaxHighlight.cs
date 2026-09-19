using System.Xml;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Loads an embedded syntax highlighting definition for an editor.
/// </summary>
public static class SyntaxHighlight
{
    private static readonly List<WeakReference<TextEditor>> Editors = [];
    private static readonly Dictionary<string, Color> DarkForegrounds = new()
    {
        ["Comment"] = Colors.LightGreen,
        ["String"] = Colors.Salmon,
        ["Keyword"] = Colors.DodgerBlue,
        ["Builtin"] = Colors.DeepSkyBlue,
        ["BuiltIn"] = Colors.DeepSkyBlue,
        ["Digits"] = Colors.LightSkyBlue,
        ["Exception"] = Colors.MediumTurquoise,
        ["Import"] = Colors.LightGreen,
        ["Jump"] = Colors.LightSteelBlue,
        ["Operator"] = Colors.MediumTurquoise,
        ["Boolean"] = Colors.LightSkyBlue,
        ["Pass"] = Colors.Silver,
        ["With"] = Colors.Plum,
        ["MethodName"] = Colors.DeepSkyBlue,
    };

    /// <summary>
    /// Defines the highlighting asset name under the application Assets directory.
    /// </summary>
    public static readonly AttachedProperty<string?> SourceProperty = AvaloniaProperty.RegisterAttached<TextEditor, string?>("Source", typeof(SyntaxHighlight));

    static SyntaxHighlight()
    {
        SourceProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
        {
            Track(editor);
            Apply(editor, e.NewValue as string);
        });
    }

    private static void Track(TextEditor editor)
    {
        Editors.RemoveAll(item => !item.TryGetTarget(out _));
        if (Editors.Any(item => item.TryGetTarget(out var existing) && ReferenceEquals(existing, editor)))
        {
            return;
        }

        Editors.Add(new(editor));
        if (Application.Current is { } app && Editors.Count == 1)
        {
            app.ActualThemeVariantChanged += (_, _) => RefreshAll();
        }
    }

    private static void RefreshAll()
    {
        foreach (var item in Editors.ToArray())
        {
            if (item.TryGetTarget(out var editor))
            {
                Apply(editor, GetSource(editor));
            }
        }
    }

    private static void Apply(TextEditor editor, string? resourceName)
    {
        if (resourceName is null)
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        if (TryLoad(resourceName, out var highlighting))
        {
            editor.SyntaxHighlighting = highlighting;
        }
    }

    private static bool TryLoad(string resourceName, out IHighlightingDefinition? highlighting)
    {
        highlighting = null;
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new($"avares://SynthMultiViewer/Assets/{resourceName}"));
            using var reader = XmlReader.Create(stream);
            var definition = HighlightingLoader.LoadXshd(reader);
            if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
            {
                ApplyDarkColors(definition);
            }

            highlighting = HighlightingLoader.Load(definition, HighlightingManager.Instance);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyDarkColors(XshdSyntaxDefinition definition)
    {
        foreach (var color in definition.Elements.OfType<XshdColor>())
        {
            if (color.Name is { } name && DarkForegrounds.TryGetValue(name, out var foreground))
            {
                color.Foreground = new SimpleHighlightingBrush(foreground);
            }
        }
    }

    /// <summary>
    /// Gets the highlighting asset name.
    /// </summary>
    public static string? GetSource(TextEditor editor) => editor.GetValue(SourceProperty);
    /// <summary>
    /// Sets the highlighting asset name.
    /// </summary>
    public static void SetSource(TextEditor editor, string? value) => editor.SetValue(SourceProperty, value);
}
