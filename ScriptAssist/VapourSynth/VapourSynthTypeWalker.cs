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
        current = ApplyUse(current, segments[0], index, bindings);
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
        expression = ExpressionParts.UnwrapParentheses(expression);
        var parts = ExpressionParts.SplitAddMul(expression);
        var node = TypeRef.Unknown;
        var last = TypeRef.Unknown;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            last = InferPart(part, bindings, index);
            if (VapourSynthTypes.IsNode(last))
            {
                node = last;
            }
        }

        if (!node.IsUnknown)
        {
            return node;
        }

        return parts.Count == 1 ? last : TypeRef.Unknown;
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
        if (bindings.CoreAliases.Contains(name) || name == "core")
        {
            return VapourSynthTypes.Core;
        }
        if (bindings.ModuleAliases.Contains(name) || name is "vs" or "vapoursynth")
        {
            return VapourSynthTypes.Module;
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
            return VapourSynthTypes.IsNode(current) ? current : TypeRef.Unknown;
        }
        var script = VapourSynthTypes.ScriptOf(current);
        if (script != null)
        {
            return bindings.ScriptModules.TryGetValue(script, out var members)
                ? HostMember(segment, members)
                : TypeRef.Unknown;
        }
        if (current == VapourSynthTypes.Module)
        {
            return ModuleMember(segment);
        }
        if (current == VapourSynthTypes.Core)
        {
            return CoreMember(segment, index);
        }
        var plugin = VapourSynthTypes.NamespaceOf(current);
        if (plugin != null)
        {
            return PluginMember(plugin, VapourSynthTypes.IsBound(current), segment, index);
        }
        if (current == VapourSynthTypes.VideoNode)
        {
            return NodeMember(segment, VapourSynthHostTypes.VideoNodeMembers, current, index);
        }
        if (current == VapourSynthTypes.AudioNode)
        {
            return NodeMember(segment, VapourSynthHostTypes.AudioNodeMembers, current, index);
        }
        if (current == VapourSynthTypes.Format)
        {
            return HostMember(segment, VapourSynthHostTypes.FormatMembers);
        }
        if (current == VapourSynthTypes.VideoFrame)
        {
            return HostMember(segment, VapourSynthHostTypes.VideoFrameMembers);
        }
        return TypeRef.Unknown;
    }

    private static TypeRef ApplyUse(TypeRef current, PathSegment segment, VapourSynthCatalogIndex index,
        DocumentBindings bindings)
    {
        if (segment.Kind == PathSegmentKind.Index)
        {
            return VapourSynthTypes.IsNode(current) ? current : TypeRef.Unknown;
        }
        if (segment.Kind != PathSegmentKind.Call)
        {
            return current;
        }

        if (current.IsUnknown || current.IsRoot)
        {
            var local = FindFunction(segment.Name, bindings);
            if (local != null)
            {
                return VapourSynthTypes.FromReturn(local.ReturnType);
            }
        }

        return current == VapourSynthTypes.Module ? VapourSynthTypes.Core : current;
    }

    private static Symbol? FindFunction(string name, DocumentBindings bindings)
    {
        foreach (var symbol in bindings.BufferSymbols)
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal) && symbol.Parameters != null)
            {
                return symbol;
            }
        }

        return null;
    }

    private static TypeRef ModuleMember(PathSegment segment)
    {
        if (segment.Name is "core" or "get_core")
        {
            return VapourSynthTypes.Core;
        }
        return HostMember(segment, VapourSynthHostTypes.ModuleMembers);
    }

    private static TypeRef CoreMember(PathSegment segment, VapourSynthCatalogIndex index)
    {
        if (index.HasNamespace(segment.Name))
        {
            return VapourSynthTypes.Plugin(segment.Name);
        }
        return HostMember(segment, VapourSynthHostTypes.CoreMembers);
    }

    private static TypeRef PluginMember(string ns, bool bound, PathSegment segment, VapourSynthCatalogIndex index)
    {
        var symbol = index.Find(ns, segment.Name);
        if (symbol == null)
        {
            return TypeRef.Unknown;
        }
        if (segment.Kind != PathSegmentKind.Call)
        {
            return TypeRef.Unknown;
        }
        _ = bound;
        return VapourSynthTypes.FromReturn(symbol.ReturnType);
    }

    private static TypeRef NodeMember(PathSegment segment, IReadOnlyList<Symbol> members, TypeRef node,
        VapourSynthCatalogIndex index)
    {
        if (index.HasBoundNamespace(segment.Name, node))
        {
            return VapourSynthTypes.Bound(segment.Name, node);
        }
        return HostMember(segment, members);
    }

    private static TypeRef HostMember(PathSegment segment, IReadOnlyList<Symbol> members)
    {
        foreach (var symbol in members)
        {
            if (!symbol.Name.Equals(segment.Name, StringComparison.Ordinal))
            {
                continue;
            }
            var nested = symbol.ReturnType != null
                ? VapourSynthTypes.ScriptOf(new TypeRef(symbol.ReturnType))
                : null;
            if (nested != null)
            {
                return segment.Kind == PathSegmentKind.Call ? TypeRef.Unknown : VapourSynthTypes.Script(nested);
            }

            if (segment.Kind == PathSegmentKind.Call || symbol.Parameters == null)
            {
                var mapped = VapourSynthTypes.FromReturn(symbol.ReturnType);
                return mapped.IsUnknown && symbol.ReturnType is "format" ? VapourSynthTypes.Format : mapped;
            }
            return TypeRef.Unknown;
        }
        return TypeRef.Unknown;
    }
}
