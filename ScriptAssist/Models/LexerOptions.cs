namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Comment and string rules for a language profile.
/// </summary>
public sealed class LexerOptions
{
    /// <summary>
    /// Gets whether <c>#</c> starts a line comment.
    /// </summary>
    public bool HashLineComments { get; init; } = true;

    /// <summary>
    /// Gets whether <c>/* */</c> block comments are recognized.
    /// </summary>
    public bool SlashStarBlocks { get; init; }

    /// <summary>
    /// Gets whether AviSynth <c>/[ ]/</c> nested blocks are recognized.
    /// </summary>
    public bool SlashBracketBlocks { get; init; }

    /// <summary>
    /// Gets whether AviSynth <c>[* *]</c> nested block comments are recognized.
    /// </summary>
    public bool StarBracketBlocks { get; init; }

    /// <summary>
    /// Gets whether single quotes start a string.
    /// </summary>
    public bool SingleQuotes { get; init; }

    /// <summary>
    /// Gets whether triple quotes start a multiline string.
    /// </summary>
    public bool TripleQuotes { get; init; }

    /// <summary>
    /// Gets whether backslash escapes apply inside strings.
    /// </summary>
    public bool StringEscapes { get; init; }

    /// <summary>
    /// Gets whether doubled quotes represent an escaped quote.
    /// </summary>
    public bool DoubledQuotes { get; init; }
}
