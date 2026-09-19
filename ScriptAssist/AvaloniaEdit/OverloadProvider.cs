using System.ComponentModel;
using AvaloniaEdit.CodeCompletion;

namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Presents overloads and the active parameter in the shared insight window.
/// </summary>
public sealed class OverloadProvider : IOverloadProvider
{
    private readonly AssistTipSize _size;
    private CallInsight _insight;
    private int _selected;
    private bool _explicit;

    /// <summary>
    /// Creates a provider for <paramref name="insight"/> using <see cref="AssistTipSize.Hover"/>.
    /// </summary>
    public OverloadProvider(CallInsight insight) : this(insight, AssistTipSize.Hover)
    {
    }

    /// <summary>
    /// Creates a provider that wraps the signature header to <paramref name="size"/>.
    /// </summary>
    public OverloadProvider(CallInsight insight, AssistTipSize size)
    {
        _insight = insight;
        _size = size.CheckNotNull();
        _selected = FirstMatchingOverload(insight);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            _selected = value.Clamp(0, Math.Max(0, Count - 1));
            _explicit = true;
            Notify();
        }
    }

    /// <inheritdoc />
    public int Count => _insight.Overloads.Count;

    /// <inheritdoc />
    public string CurrentIndexText => $"{SelectedIndex + 1} / {Count}";

    /// <inheritdoc />
    public object CurrentHeader =>
        CompletionData.HintBlock(_insight.Overloads[SelectedIndex].Signature, _size);

    /// <inheritdoc />
    public object CurrentContent => ActiveParameterText(_insight, SelectedIndex);

    /// <summary>
    /// Replaces the displayed insight. Selection is kept when the overloads are the same call.
    /// </summary>
    internal void Update(CallInsight insight)
    {
        var selected = _selected;
        var same = SameCall(_insight, insight);
        _insight = insight;
        if (same && _explicit)
        {
            _selected = selected.Clamp(0, Math.Max(0, Count - 1));
        }
        else
        {
            _selected = FirstMatchingOverload(insight);
            _explicit = false;
        }

        Notify();
    }

    private void Notify()
    {
        var handler = PropertyChanged;
        if (handler == null)
        {
            return;
        }

        handler(this, new(nameof(SelectedIndex)));
        handler(this, new(nameof(Count)));
        handler(this, new(nameof(CurrentIndexText)));
        handler(this, new(nameof(CurrentHeader)));
        handler(this, new(nameof(CurrentContent)));
    }

    /// <summary>
    /// Describes the argument under the caret, including extra and repeating parameters.
    /// </summary>
    internal static string ActiveParameterText(CallInsight insight, int selected = -1)
    {
        if (selected < 0)
        {
            selected = FirstMatchingOverload(insight);
        }

        selected = selected.Clamp(0, Math.Max(0, insight.Overloads.Count - 1));
        var overload = insight.Overloads[selected];
        var parameters = overload.Parameters;
        if (parameters == null)
        {
            return "Parameters unknown";
        }

        if (parameters.Length == 0)
        {
            return "No parameters";
        }

        var index = insight.GetActiveParameter(selected);
        if (index >= 0 && index < parameters.Length && !ParameterNames.IsSeparator(parameters[index]))
        {
            return "Parameter " + SlotNumber(parameters, index) + ": " + parameters[index];
        }

        if (insight.Keyword == null && Repeats(parameters[^1]))
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

            var index = insight.GetActiveParameter(i);
            if (index >= 0 && index < parameters.Length ||
                insight.Keyword == null && parameters.Length > 0 && Repeats(parameters[^1]))
            {
                return i;
            }
        }

        return 0;
    }

    private static bool SameCall(CallInsight left, CallInsight right)
    {
        if (left.Overloads.Count != right.Overloads.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Overloads.Count; i++)
        {
            if (!SameOverload(left.Overloads[i], right.Overloads[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameOverload(Symbol left, Symbol right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (!left.Name.Equals(right.Name, StringComparison.Ordinal) || left.Kind != right.Kind ||
            left.ImplicitLast != right.ImplicitLast ||
            !string.Equals(left.ReturnType, right.ReturnType, StringComparison.Ordinal))
        {
            return false;
        }

        var a = left.Parameters;
        var b = right.Parameters;
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
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
