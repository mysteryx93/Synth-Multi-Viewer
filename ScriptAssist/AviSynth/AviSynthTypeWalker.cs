namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Types AviSynth identifiers, calls, and clip copies.
/// </summary>
internal static class AviSynthTypeWalker
{
    /// <summary>
    /// Types <paramref name="segments"/> left to right.
    /// </summary>
    public static TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings, IReadOnlyList<Symbol> catalog)
    {
        if (segments.Count == 0)
        {
            return TypeRef.Root;
        }

        _ = catalog;
        var first = segments[0];
        var current = Resolve(first.Name, bindings);
        if (first.Kind == PathSegmentKind.Call)
        {
            current = AviSynthInternals.ReturnOf(first.Name);
        }
        else if (first.Kind == PathSegmentKind.Index)
        {
            current = Index(current);
        }

        for (var i = 1; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Kind == PathSegmentKind.Index)
            {
                if (segment.Name.Length > 0)
                {
                    if (current != AviSynthTypes.Clip)
                    {
                        return TypeRef.Unknown;
                    }

                    current = AviSynthInternals.ReturnOf(segment.Name);
                }

                current = Index(current);
                continue;
            }

            if (current != AviSynthTypes.Clip)
            {
                return TypeRef.Unknown;
            }

            current = AviSynthInternals.ReturnOf(segment.Name);
        }

        return current;
    }

    private static TypeRef Resolve(string name, DocumentBindings bindings)
    {
        if (name.Equals("last", StringComparison.OrdinalIgnoreCase))
        {
            return AviSynthTypes.Clip;
        }

        return bindings.Names.TryGetValue(name, out var type) ? type : TypeRef.Unknown;
    }

    private static TypeRef Index(TypeRef current) =>
        current == AviSynthTypes.Clip ? AviSynthTypes.Clip : TypeRef.Unknown;
}
