namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Wrap and truncation limits for a completion hint or hover tooltip.
/// </summary>
public sealed class AssistTipSize
{
    /// <summary>
    /// Default completion side-panel size.
    /// </summary>
    public static AssistTipSize Hint { get; } = new();

    /// <summary>
    /// Default hover tooltip size. Wider than <see cref="Hint"/> so long signatures wrap less.
    /// </summary>
    public static AssistTipSize Hover { get; } = new()
    {
        MaxWidth = 960,
        MaxLines = 16,
        MaxCharacters = 2000
    };

    /// <summary>
    /// Gets the wrap width in device-independent pixels.
    /// </summary>
    public double MaxWidth { get; init; } = 560;

    /// <summary>
    /// Gets the maximum visible lines before ellipsis.
    /// </summary>
    public int MaxLines { get; init; } = 8;

    /// <summary>
    /// Gets the maximum characters kept before truncation.
    /// </summary>
    public int MaxCharacters { get; init; } = 400;
}
