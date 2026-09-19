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
    private object? _header;
    private string? _content;

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
            _explicit = true;
            var selected = value.Clamp(0, Math.Max(0, Count - 1));
            if (selected == _selected)
            {
                return;
            }

            _selected = selected;
            _header = null;
            _content = null;
            Notify(nameof(SelectedIndex), nameof(CurrentIndexText), nameof(CurrentHeader), nameof(CurrentContent));
        }
    }

    /// <inheritdoc />
    public int Count => _insight.Overloads.Count;

    /// <inheritdoc />
    public string CurrentIndexText => $"{SelectedIndex + 1} / {Count}";

    /// <inheritdoc />
    public object CurrentHeader =>
        _header ??= CompletionData.HintBlock(_insight.Overloads[SelectedIndex].Signature, _size);

    /// <inheritdoc />
    public object CurrentContent => _content ??= ActiveParameterText(_insight, SelectedIndex);

    /// <summary>
    /// Replaces the displayed insight. Selection is kept when the overloads are the same call.
    /// </summary>
    internal void Update(CallInsight insight)
    {
        var previousIndex = _selected;
        var previousCount = Count;
        var previousHeader = Count == 0 ? "" : _insight.Overloads[_selected].Signature;
        var previousContent = ActiveParameterText(_insight, _selected);
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

        var header = Count == 0 ? "" : _insight.Overloads[_selected].Signature;
        if (!string.Equals(previousHeader, header, StringComparison.Ordinal) || previousIndex != _selected)
        {
            _header = null;
            Notify(nameof(CurrentHeader));
        }

        var content = ActiveParameterText(_insight, _selected);
        if (!string.Equals(previousContent, content, StringComparison.Ordinal))
        {
            _content = null;
            Notify(nameof(CurrentContent));
        }

        if (previousIndex != _selected)
        {
            Notify(nameof(SelectedIndex), nameof(CurrentIndexText));
        }

        if (previousCount != Count)
        {
            Notify(nameof(Count), nameof(CurrentIndexText));
        }
    }

    private void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler == null)
        {
            return;
        }

        foreach (var name in names)
        {
            handler(this, new(name));
        }
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
