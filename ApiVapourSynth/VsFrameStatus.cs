namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Describes the result and queue state of an asynchronous frame request.
/// </summary>
public class VsFrameStatus : EventArgs
{
    /// <summary>
    /// Creates an empty frame request status.
    /// </summary>
    public VsFrameStatus() { }

    /// <summary>
    /// Creates a request status for the specified frame index.
    /// </summary>
    public VsFrameStatus(int index)
    {
        Index = index;
    }

    /// <summary>
    /// Gets or sets the requested zero-based frame index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the frame reference, which is released after the frame callback returns.
    /// </summary>
    public VsFrame? Frame { get; set; }

    /// <summary>
    /// Gets or sets the native error message, or null for a successful request.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the request state in the processing queue.
    /// </summary>
    public VsFrameState State { get; set; } = VsFrameState.Requested;
}
