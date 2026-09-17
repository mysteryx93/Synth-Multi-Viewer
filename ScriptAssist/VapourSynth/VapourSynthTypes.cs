namespace HanumanInstitute.ScriptAssist.VapourSynth;

/// <summary>
/// Type identifiers used by the VapourSynth profile.
/// </summary>
public static class VapourSynthTypes
{
    /// <summary>The vapoursynth module.</summary>
    public static TypeRef Module { get; } = new("vs");

    /// <summary>The script core.</summary>
    public static TypeRef Core { get; } = new("core");

    /// <summary>A video clip.</summary>
    public static TypeRef VideoNode { get; } = new("vnode");

    /// <summary>An audio clip.</summary>
    public static TypeRef AudioNode { get; } = new("anode");

    /// <summary>A video format object.</summary>
    public static TypeRef Format { get; } = new("format");

    /// <summary>A video frame.</summary>
    public static TypeRef VideoFrame { get; } = new("vframe");

    /// <summary>An integer.</summary>
    public static TypeRef Int { get; } = new("int");

    /// <summary>A floating-point number.</summary>
    public static TypeRef Float { get; } = new("float");

    /// <summary>A boolean.</summary>
    public static TypeRef Bool { get; } = new("bool");

    /// <summary>A string.</summary>
    public static TypeRef String { get; } = new("string");

    /// <summary>A plugin namespace on the core.</summary>
    public static TypeRef Plugin(string ns) => new("plugin:" + ns);

    /// <summary>A plugin namespace bound to a node.</summary>
    public static TypeRef Bound(string ns) => Bound(ns, VideoNode);

    /// <summary>A plugin namespace bound to a video or audio node.</summary>
    public static TypeRef Bound(string ns, TypeRef node) =>
        new((node == AudioNode ? "bound-anode:" : "bound:") + ns);

    /// <summary>An imported Python script module.</summary>
    public static TypeRef Script(string module) => new("script:" + module);

    /// <summary>Gets the imported module id from a script type.</summary>
    public static string? ScriptOf(TypeRef type) =>
        type.Id.StartsWith("script:", StringComparison.Ordinal) ? type.Id["script:".Length..] : null;

    /// <summary>Gets the plugin namespace from a plugin or bound type.</summary>
    public static string? NamespaceOf(TypeRef type)
    {
        if (type.Id.StartsWith("plugin:", StringComparison.Ordinal))
        {
            return type.Id["plugin:".Length..];
        }

        if (type.Id.StartsWith("bound-anode:", StringComparison.Ordinal))
        {
            return type.Id["bound-anode:".Length..];
        }

        if (type.Id.StartsWith("bound:", StringComparison.Ordinal))
        {
            return type.Id["bound:".Length..];
        }

        return null;
    }

    /// <summary>Gets whether the type is a bound plugin.</summary>
    public static bool IsBound(TypeRef type) =>
        type.Id.StartsWith("bound:", StringComparison.Ordinal) ||
        type.Id.StartsWith("bound-anode:", StringComparison.Ordinal);

    /// <summary>Gets the node a bound plugin was taken from.</summary>
    public static TypeRef BoundNode(TypeRef type) =>
        type.Id.StartsWith("bound-anode:", StringComparison.Ordinal) ? AudioNode : VideoNode;

    /// <summary>Gets whether the type is a video or audio node.</summary>
    public static bool IsNode(TypeRef type) => type == VideoNode || type == AudioNode;

    /// <summary>Maps a catalog return string to a type.</summary>
    public static TypeRef FromReturn(string? returnType)
    {
        if (!returnType.HasValue() || returnType == "any")
        {
            return TypeRef.Unknown;
        }

        var parts = returnType.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 1)
        {
            return TypeRef.Unknown;
        }

        var part = parts[0];
        var colon = part.IndexOf(':');
        var type = colon < 0 ? part : part[(colon + 1)..];
        type = type.Replace("[]", "", StringComparison.Ordinal).Replace(":opt", "", StringComparison.Ordinal)
            .Replace(":empty", "", StringComparison.Ordinal);
        return type switch
        {
            "vnode" or "VideoNode" => VideoNode,
            "anode" or "AudioNode" => AudioNode,
            "int" => Int,
            "float" => Float,
            "bool" => Bool,
            "data" or "string" or "str" => String,
            "vframe" or "VideoFrame" => VideoFrame,
            "func" => new("func"),
            _ => TypeRef.Unknown
        };
    }

    /// <summary>Display name for hover and completion hints; null when there is nothing to show.</summary>
    public static string? Display(TypeRef type)
    {
        if (type.IsUnknown || type.IsRoot)
        {
            return null;
        }

        if (type == VideoNode)
        {
            return "VideoNode";
        }

        if (type == AudioNode)
        {
            return "AudioNode";
        }

        if (type == Core)
        {
            return "Core";
        }

        if (type == Module)
        {
            return "vapoursynth";
        }

        if (type == Format)
        {
            return "VideoFormat";
        }

        if (type == VideoFrame)
        {
            return "VideoFrame";
        }

        if (type == Int)
        {
            return "int";
        }

        if (type == Float)
        {
            return "float";
        }

        if (type == Bool)
        {
            return "bool";
        }

        if (type == String)
        {
            return "str";
        }

        var ns = NamespaceOf(type);
        if (ns != null)
        {
            return (IsBound(type) ? "bound plugin " : "plugin ") + ns;
        }

        var script = ScriptOf(type);
        return script != null ? "module " + script : type.Id;
    }

    /// <summary>Display name for a catalog return string such as <c>vnode</c> or <c>Fraction</c>.</summary>
    public static string? DisplayReturn(string? returnType)
    {
        if (!returnType.HasValue())
        {
            return null;
        }

        var mapped = FromReturn(returnType);
        if (!mapped.IsUnknown)
        {
            return Display(mapped);
        }

        return returnType == "format" ? "VideoFormat" : returnType;
    }

    /// <summary>Maps a Python annotation or last identifier to a type.</summary>
    public static TypeRef FromAnnotation(string? annotation)
    {
        if (!annotation.HasValue())
        {
            return TypeRef.Unknown;
        }

        var text = annotation.Trim();
        var pipe = text.IndexOf('|');
        if (pipe > 0)
        {
            text = text[..pipe].Trim();
        }

        if (text.StartsWith("Optional[", StringComparison.Ordinal) && text.EndsWith(']'))
        {
            text = text["Optional[".Length..^1].Trim();
        }

        var last = text.LastIndexOf('.');
        if (last >= 0)
        {
            text = text[(last + 1)..];
        }

        return text switch
        {
            "VideoNode" => VideoNode,
            "AudioNode" => AudioNode,
            "int" => Int,
            "float" => Float,
            "bool" => Bool,
            "str" or "string" => String,
            _ => TypeRef.Unknown
        };
    }
}
