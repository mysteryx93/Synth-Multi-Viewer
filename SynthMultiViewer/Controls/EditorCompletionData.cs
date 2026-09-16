using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using HanumanInstitute.SynthMultiViewer.Services.Completion;

namespace HanumanInstitute.SynthMultiViewer.Controls;

/// <summary>
/// Adapts a snapshot completion to AvaloniaEdit's live replacement segment.
/// </summary>
public sealed class EditorCompletionData(EditorCompletion item) : ICompletionData
{
    /// <inheritdoc />
    public IImage Image => null!;

    /// <inheritdoc />
    public string Text => item.InsertionText;

    /// <inheritdoc />
    public object Content => Text;

    /// <inheritdoc />
    public object Description
    {
        get
        {
            var text = HintText(item);
            return string.IsNullOrEmpty(text)
                ? null!
                : new TextBlock
                {
                    Text = text,
                    MaxWidth = HintMaxWidth,
                    MaxLines = HintMaxLines,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
        }
    }

    internal const double HintMaxWidth = 560;
    internal const int HintMaxLines = 8;

    /// <summary>
    /// Parameter list only; the list already shows the name, and parameter info shows the full signature.
    /// </summary>
    internal static string? HintText(EditorCompletion item)
    {
        if (item.Kind != CompletionKind.Function)
        {
            return null;
        }

        var signature = item.Signature;
        var open = signature.IndexOf('(');
        var close = signature.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return TruncateHint(signature);
        }

        var inside = signature[(open + 1)..close].Trim();
        return inside.Length == 0 ? "No parameters" : TruncateHint(inside);
    }

    /// <summary>
    /// Caps extreme native signatures; wrapping and line limit handle ordinary length.
    /// </summary>
    internal static string TruncateHint(string text, int max = 400)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..(max - 1)] + "…";
    }

    /// <inheritdoc />
    public double Priority => 0;

    /// <inheritdoc />
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, item.InsertionText);
}

/// <summary>
/// Presents overloads and the active parameter in the shared insight window.
/// </summary>
public sealed class EditorOverloadProvider(CallInsight insight) : IOverloadProvider
{
    private int _selected = FirstMatchingOverload(insight);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            _selected = Math.Clamp(value, 0, Math.Max(0, Count - 1));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    /// <inheritdoc />
    public int Count => insight.Overloads.Count;

    /// <inheritdoc />
    public string CurrentIndexText => $"{SelectedIndex + 1} / {Count}";

    /// <inheritdoc />
    public object CurrentHeader => insight.Overloads[SelectedIndex].Signature;

    /// <inheritdoc />
    public object CurrentContent => ActiveParameterText(insight, SelectedIndex);

    /// <summary>
    /// Describes the argument under the caret, including extra and repeating parameters.
    /// </summary>
    internal static string ActiveParameterText(CallInsight insight, int selected = -1)
    {
        if (selected < 0)
        {
            selected = FirstMatchingOverload(insight);
        }

        var parameters = insight.Overloads[Math.Clamp(selected, 0, Math.Max(0, insight.Overloads.Count - 1))].Parameters;
        if (parameters == null)
        {
            return "Parameters unknown";
        }

        if (parameters.Length == 0)
        {
            return "No parameters";
        }

        var index = insight.ActiveParameter;
        if (insight.ImplicitClip && parameters[0].StartsWith("clip", StringComparison.OrdinalIgnoreCase))
        {
            index++;
        }

        if (index < parameters.Length)
        {
            return "Parameter " + (index + 1) + ": " + parameters[index];
        }

        if (Repeats(parameters[^1]))
        {
            return "Parameter " + parameters.Length + ": " + parameters[^1];
        }

        return "No more parameters";
    }

    internal static int FirstMatchingOverload(CallInsight insight)
    {
        for (var i = 0; i < insight.Overloads.Count; i++)
        {
            var parameters = insight.Overloads[i].Parameters;
            if (parameters == null)
            {
                return i;
            }

            var index = insight.ActiveParameter;
            if (insight.ImplicitClip && parameters.Length > 0 &&
                parameters[0].StartsWith("clip", StringComparison.OrdinalIgnoreCase))
            {
                index++;
            }

            if (index < parameters.Length || (parameters.Length > 0 && Repeats(parameters[^1])))
            {
                return i;
            }
        }

        return 0;
    }

    private static bool Repeats(string parameter)
    {
        var type = parameter;
        var space = parameter.IndexOf(' ');
        if (space >= 0)
        {
            type = parameter[..space];
        }

        return type.EndsWith('*') || type.EndsWith('+') || type.Contains("[]", StringComparison.Ordinal);
    }
}
