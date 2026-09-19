namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// Maps whether the current call omits the first clip or node argument.
/// </summary>
internal interface ICallReceiver
{
    /// <summary>
    /// Returns true when the first catalog clip is supplied by <c>last</c> or the receiver,
    /// not by the argument under the caret.
    /// </summary>
    bool OmitsFirstClip(CallResolution resolved, string firstArgument, DocumentBindings bindings,
        IReadOnlyList<Symbol> catalog, CancellationToken token);
}
