namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// A catalog or buffer symbol with optional parameters and return type.
/// </summary>
public sealed record Symbol(
    string Name,
    string[]? Parameters,
    SymbolKind Kind = SymbolKind.Function,
    bool ImplicitLast = false,
    string? ReturnType = null)
{
    /// <summary>
    /// Gets the last dotted segment of <see cref="Name"/>. Catalog identity stays on
    /// <see cref="Name"/>; hover, insight, and completion insert this.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var last = Name.LastIndexOf('.');
            return last < 0 ? Name : Name[(last + 1)..];
        }
    }

    /// <summary>
    /// Gets the display signature.
    /// </summary>
    public string Signature
    {
        get
        {
            if (Kind is SymbolKind.Property or SymbolKind.Local)
            {
                return ReturnType.HasValue() ? Name + ": " + ReturnType : Name;
            }

            if (Kind != SymbolKind.Function)
            {
                return Name;
            }

            var inside = Parameters == null ? "parameters unknown" : string.Join(", ", Parameters);
            var suffix = ImplicitLast ? " [implicit last]" : "";
            var call = DisplayName + "(" + inside + ")" + suffix;
            if (!ReturnType.HasValue())
            {
                return call;
            }

            var ret = ReturnType.TrimEnd(';').Trim();
            return ret.Length == 0 ? call : call + " -> " + ret;
        }
    }
}
