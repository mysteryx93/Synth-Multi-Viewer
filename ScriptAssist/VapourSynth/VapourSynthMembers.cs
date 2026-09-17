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
            return Concat(VapourSynthHostTypes.VideoNodeMembers, index.BoundNamespaces);
        }
        if (type == VapourSynthTypes.AudioNode)
        {
            return Concat(VapourSynthHostTypes.AudioNodeMembers, index.BoundNamespaces);
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
            return index.Functions(ns, VapourSynthTypes.IsBound(type));
        }
        return [];
    }

    private static IReadOnlyList<Symbol> Root(IReadOnlyList<Symbol> keywords, DocumentBindings bindings)
    {
        var items = new List<Symbol>(keywords.Count + bindings.Names.Count + bindings.BufferSymbols.Count);
        items.AddRange(keywords);
        items.AddRange(bindings.BufferSymbols);
        foreach (var pair in bindings.Names)
        {
            items.Add(new Symbol(pair.Key, null, SymbolKind.Local,
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
