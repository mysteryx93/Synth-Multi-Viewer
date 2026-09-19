namespace HanumanInstitute.ScriptAssist.AvaloniaEdit;

/// <summary>
/// Wrap limits for assistance popups.
/// </summary>
public sealed class EditorAssistOptions
{
    /// <summary>
    /// Gets wrap limits for the completion side panel. Defaults to <see cref="AssistTipSize.Hint"/>.
    /// </summary>
    public AssistTipSize Hint { get; init; } = AssistTipSize.Hint;

    /// <summary>
    /// Gets wrap limits for identifier hover and call-insight headers. Defaults to
    /// <see cref="AssistTipSize.Hover"/>.
    /// </summary>
    public AssistTipSize Hover { get; init; } = AssistTipSize.Hover;
}
