namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Links nested function scopes and maps each statement to its innermost scope.
/// </summary>
internal static class VapourSynthScopeLinks
{
    public static void LinkEnclosing(List<BindingScope> scopes)
    {
        var stack = new List<BindingScope>();
        foreach (var scope in scopes)
        {
            while (stack.Count > 0 && stack[^1].End < scope.Start)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            scope.Enclosing = stack.Count == 0 ? null : stack[^1];
            stack.Add(scope);
        }
    }

    public static BindingScope?[] MapInnermost(IReadOnlyList<StatementScanner.Span> statements,
        IReadOnlyList<BindingScope> scopes)
    {
        var inner = new BindingScope?[statements.Count];
        var si = 0;
        var stack = new List<BindingScope>();
        for (var i = 0; i < statements.Count; i++)
        {
            var start = statements[i].Start;
            while (si < scopes.Count && scopes[si].Start <= start)
            {
                stack.Add(scopes[si]);
                si++;
            }

            while (stack.Count > 0 && stack[^1].End < start)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            inner[i] = stack.Count == 0 ? null : stack[^1];
        }

        return inner;
    }
}
