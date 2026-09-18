using System.ComponentModel;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Presents overloads and the active parameter in the shared insight window.
/// </summary>
public sealed class OverloadProvider(CallInsight insight) : IOverloadProvider
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
            _selected = value.Clamp(0, Math.Max(0, Count - 1));
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

        var parameters = insight.Overloads[selected.Clamp(0, Math.Max(0, insight.Overloads.Count - 1))].Parameters;
        if (parameters == null)
        {
            return "Parameters unknown";
        }

        if (parameters.Length == 0)
        {
            return "No parameters";
        }

        var index = insight.ActiveParameter;
        if (index >= 0 && index < parameters.Length && !ParameterNames.IsSeparator(parameters[index]))
        {
            return "Parameter " + SlotNumber(parameters, index) + ": " + parameters[index];
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
            if (index >= 0 && index < parameters.Length || (parameters.Length > 0 && Repeats(parameters[^1])))
            {
                return i;
            }
        }

        return 0;
    }

    private static int SlotNumber(string[] parameters, int physical)
    {
        var n = 0;
        for (var i = 0; i <= physical && i < parameters.Length; i++)
        {
            if (!ParameterNames.IsSeparator(parameters[i]))
            {
                n++;
            }
        }

        return Math.Max(n, 1);
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
