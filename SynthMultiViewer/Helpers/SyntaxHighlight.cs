using System.Xml;
using Avalonia;
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

        Editors.Add(new WeakReference<TextEditor>(editor));
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

        if (Application.Current?.ActualThemeVariant == ThemeVariant.Dark)
        {
            var darkName = Path.GetFileNameWithoutExtension(resourceName) + ".Dark" + Path.GetExtension(resourceName);
            if (TryLoad(darkName, out var darkHighlighting))
            {
                editor.SyntaxHighlighting = darkHighlighting;
                return;
            }
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
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://SynthMultiViewer/Assets/{resourceName}"));
            using var reader = XmlReader.Create(stream);
            highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return true;
        }
        catch
        {
            return false;
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
