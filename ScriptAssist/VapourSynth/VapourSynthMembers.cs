namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Lists members offered for a typed VapourSynth receiver.
/// </summary>
internal static class VapourSynthMembers
{
    /// <summary>
    /// Catalog functions stay off the statement root so <c>Crop</c> does not leak at the start of a line.
    /// </summary>
    public static IReadOnlyList<Symbol> Of(TypeRef type, IReadOnlyList<Symbol> keywords, DocumentBindings bindings, VapourSynthCatalogIndex index)
    {
        if (type.IsRoot)
        {
            return Root(keywords, bindings);
        }
        var script = VapourSynthTypes.ScriptOf(type);
        if (script != null)
        {
            return bindings.ScriptModules.TryGetValue(script, out var members) ? members : [];
        }
        if (type == VapourSynthTypes.Module)
        {
            return VapourSynthHostTypes.ModuleMembers;
        }
        if (type == VapourSynthTypes.Core)
        {
            return Concat(VapourSynthHostTypes.CoreMembers, index.Namespaces);
        }
        if (type == VapourSynthTypes.VideoNode)
        {
            return Concat(VapourSynthHostTypes.VideoNodeMembers, index.BoundNamespaces(type));
        }
        if (type == VapourSynthTypes.AudioNode)
        {
            return Concat(VapourSynthHostTypes.AudioNodeMembers, index.BoundNamespaces(type));
        }
        if (type == VapourSynthTypes.Format)
        {
            return VapourSynthHostTypes.FormatMembers;
        }
        if (type == VapourSynthTypes.VideoFrame)
        {
            return VapourSynthHostTypes.VideoFrameMembers;
        }

        var ns = VapourSynthTypes.NamespaceOf(type);
        if (ns != null)
        {
            return index.Functions(ns, VapourSynthTypes.IsBound(type),
                VapourSynthTypes.IsBound(type) ? VapourSynthTypes.BoundNode(type) : default);
        }
        return [];
    }

    /// <summary>
    /// Finds a member of <paramref name="receiver"/> by the identifier used in source.
    /// Bound plugin lookups stay filtered to the receiver node.
    /// </summary>
    public static Symbol? Find(TypeRef receiver, string name, DocumentBindings bindings,
        VapourSynthCatalogIndex index)
    {
        if (name.Length == 0)
        {
            return null;
        }

        foreach (var symbol in Of(receiver, [], bindings, index))
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal) ||
                symbol.Name.EndsWith('.' + name, StringComparison.Ordinal))
            {
                return symbol;
            }
        }

        return null;
    }

    private static IReadOnlyList<Symbol> Root(IReadOnlyList<Symbol> keywords, DocumentBindings bindings)
    {
        var items = new List<Symbol>(keywords.Count + bindings.Names.Count + bindings.BufferSymbols.Count);
        items.AddRange(keywords);
        foreach (var symbol in bindings.BufferSymbols)
        {
            if (!bindings.Names.ContainsKey(symbol.Name))
            {
                items.Add(symbol);
            }
        }

        foreach (var pair in bindings.Names)
        {
            items.Add(new(pair.Key, null, SymbolKind.Local,
                ReturnType: VapourSynthTypes.Display(pair.Value) ?? pair.Value.Id));
        }
        return items;
    }

    private static IReadOnlyList<Symbol> Concat(IReadOnlyList<Symbol> left, IReadOnlyList<Symbol> right)
    {
        if (right.Count == 0) { return left; }

        var items = new List<Symbol>(left.Count + right.Count);
        items.AddRange(left);
        items.AddRange(right);
        return items;
    }
}
