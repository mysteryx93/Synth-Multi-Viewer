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

        if (receiver.IsRoot)
        {
            foreach (var symbol in bindings.BufferSymbols)
            {
                if (symbol.Name.Equals(name, StringComparison.Ordinal))
                {
                    return symbol;
                }
            }

            return bindings.Names.ContainsKey(name)
                ? new(name, null, SymbolKind.Local)
                : null;
        }

        var script = VapourSynthTypes.ScriptOf(receiver);
        if (script != null)
        {
            return bindings.ScriptModules.TryGetValue(script, out var members)
                ? Named(members, name)
                : null;
        }

        if (receiver == VapourSynthTypes.Module)
        {
            return Named(VapourSynthHostTypes.ModuleMembers, name);
        }

        if (receiver == VapourSynthTypes.Core)
        {
            return Named(VapourSynthHostTypes.CoreMembers, name) ??
                index.FindNamespace(name, false);
        }

        if (receiver == VapourSynthTypes.VideoNode)
        {
            return Named(VapourSynthHostTypes.VideoNodeMembers, name) ??
                index.FindNamespace(name, true, receiver);
        }

        if (receiver == VapourSynthTypes.AudioNode)
        {
            return Named(VapourSynthHostTypes.AudioNodeMembers, name) ??
                index.FindNamespace(name, true, receiver);
        }

        if (receiver == VapourSynthTypes.Format)
        {
            return Named(VapourSynthHostTypes.FormatMembers, name);
        }

        if (receiver == VapourSynthTypes.VideoFrame)
        {
            return Named(VapourSynthHostTypes.VideoFrameMembers, name);
        }

        var ns = VapourSynthTypes.NamespaceOf(receiver);
        return ns == null
            ? null
            : index.FindFunction(ns, name, VapourSynthTypes.IsBound(receiver),
                VapourSynthTypes.IsBound(receiver) ? VapourSynthTypes.BoundNode(receiver) : default);
    }

    private static Symbol? Named(IReadOnlyList<Symbol> symbols, string name)
    {
        foreach (var symbol in symbols)
        {
            if (symbol.Name.Equals(name, StringComparison.Ordinal))
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
