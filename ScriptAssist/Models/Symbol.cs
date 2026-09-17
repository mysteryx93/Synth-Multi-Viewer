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
            return Name + "(" + inside + ")" + suffix;
        }
    }
}
