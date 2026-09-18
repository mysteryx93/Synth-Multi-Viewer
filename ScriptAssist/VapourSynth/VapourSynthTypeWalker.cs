namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Types dotted, called, and indexed VapourSynth expressions.
/// </summary>
internal static class VapourSynthTypeWalker
{
    /// <summary>
    /// Types <paramref name="segments"/> left to right.
    /// </summary>
    public static TypeRef TypeOf(IReadOnlyList<PathSegment> segments, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        if (segments.Count == 0)
        {
            return TypeRef.Root;
        }

        var current = ResolveName(segments[0].Name, bindings);
        current = ApplyUse(current, segments[0], bindings);
        for (var i = 1; i < segments.Count; i++)
        {
            current = Step(current, segments[i], index, bindings);
        }

        return current;
    }

    /// <summary>
    /// Types a right-hand side, including clip copy through <c>+</c> and <c>*</c>.
    /// </summary>
    public static TypeRef Infer(string expression, DocumentBindings bindings, VapourSynthCatalogIndex index)
    {
        var unwrapped = ExpressionParts.UnwrapParentheses(expression);
        if (IsInteger(unwrapped))
        {
            return VapourSynthTypes.Int;
        }

        if (IsFloat(unwrapped))
        {
            return VapourSynthTypes.Float;
        }

        if (unwrapped is "True" or "False")
        {
            return VapourSynthTypes.Bool;
        }

        if (IsStringLiteral(unwrapped))
        {
            return VapourSynthTypes.String;
        }

        var parts = ExpressionParts.SplitAddMul(unwrapped);
        if (parts.Count == 1)
        {
            return InferPart(expression, bindings, index);
        }

        var node = TypeRef.Unknown;
        var last = TypeRef.Unknown;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            last = InferPart(ExpressionParts.GroupOperand(expression, part), bindings, index);
            if (VapourSynthTypes.IsNode(last))
            {
                node = last;
            }
        }

        if (!node.IsUnknown)
        {
            return node;
        }

        return TypeRef.Unknown;
    }

    private static TypeRef InferPart(string part, DocumentBindings bindings, VapourSynthCatalogIndex index)
    {
        if (IsInteger(part))
        {
            return VapourSynthTypes.Int;
        }

        if (IsFloat(part))
        {
            return VapourSynthTypes.Float;
        }

        if (part is "True" or "False")
        {
            return VapourSynthTypes.Bool;
        }

        if (IsStringLiteral(part))
        {
            return VapourSynthTypes.String;
        }

        var segments = ExpressionReader.Parse(part);
        if (segments.Count == 0)
        {
            return TypeRef.Unknown;
        }

        var type = TypeOf(segments, bindings, index);
        return type.IsRoot ? TypeRef.Unknown : type;
    }

    private static bool IsInteger(string part)
    {
        if (part.Length == 0)
        {
            return false;
        }

        var i = part[0] == '-' ? 1 : 0;
        if (i >= part.Length)
        {
            return false;
        }

        while (i < part.Length)
        {
            if (!char.IsDigit(part[i]))
            {
                return false;
            }

            i++;
        }

        return true;
    }

    private static bool IsFloat(string part) =>
        part.IndexOf('.') >= 0 &&
        double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static bool IsStringLiteral(string part) =>
        part.Length >= 2 && part[0] is '"' or '\'' && part[^1] == part[0];

    private static TypeRef ResolveName(string name, DocumentBindings bindings)
    {
        if (bindings.Names.TryGetValue(name, out var typed))
        {
            return typed;
        }

        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
            {
                return VapourSynthTypes.Function(symbol);
            }
        }

        return TypeRef.Unknown;
    }

    private static TypeRef Step(TypeRef current, PathSegment segment, VapourSynthCatalogIndex index,
        DocumentBindings bindings)
    {
        if (current.IsUnknown)
        {
            return TypeRef.Unknown;
        }
        if (segment.Kind == PathSegmentKind.Index)
        {
            if (segment.Name.Length > 0)
            {
                current = Step(current, new PathSegment { Name = segment.Name, Kind = PathSegmentKind.Name },
                    index, bindings);
            }

            return Index(current);
        }

        if (current == VapourSynthTypes.Module && segment.Name is "core" or "get_core")
        {
            return VapourSynthTypes.Core;
        }

        var symbol = VapourSynthMembers.Find(current, segment.Name, bindings, index);
        if (symbol == null)
        {
            return TypeRef.Unknown;
        }

        if (symbol.Kind == SymbolKind.Namespace)
        {
            if (current == VapourSynthTypes.Core)
            {
                return VapourSynthTypes.Plugin(segment.Name);
            }

            if (VapourSynthTypes.IsNode(current))
            {
                return VapourSynthTypes.Bound(segment.Name, current);
            }
        }

        return ApplyMember(symbol, segment, VapourSynthTypes.IsBound(current));
    }

    private static TypeRef ApplyUse(TypeRef current, PathSegment segment, DocumentBindings bindings)
    {
        if (segment.Kind == PathSegmentKind.Index)
        {
            return Index(current);
        }
        if (segment.Kind != PathSegmentKind.Call)
        {
            return current;
        }

        var snapshot = VapourSynthTypes.FunctionSymbol(current);
        if (snapshot != null)
        {
            return ReturnOf(snapshot);
        }

        if (current == VapourSynthTypes.Module)
        {
            return VapourSynthTypes.Core;
        }

        return TypeRef.Unknown;
    }

    private static TypeRef ReturnOf(Symbol symbol) => VapourSynthTypes.FromReturn(symbol.ReturnType);

    private static TypeRef ApplyMember(Symbol symbol, PathSegment segment, bool bound)
    {
        var nested = symbol.ReturnType != null
            ? VapourSynthTypes.ScriptOf(new TypeRef(symbol.ReturnType))
            : null;
        if (nested != null)
        {
            return segment.Kind == PathSegmentKind.Call ? TypeRef.Unknown : VapourSynthTypes.Script(nested);
        }

        if (segment.Kind == PathSegmentKind.Call)
        {
            return symbol.Parameters == null ? TypeRef.Unknown : ReturnOf(symbol);
        }

        if (symbol.Parameters == null)
        {
            return ReturnOf(symbol);
        }

        return VapourSynthTypes.Function(symbol, bound);
    }

    private static TypeRef Index(TypeRef current) =>
        VapourSynthTypes.IsNode(current) ? current : TypeRef.Unknown;
}
