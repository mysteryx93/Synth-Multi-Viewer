namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Owns a native video frame reference and exposes its image planes.
/// </summary>
public class VsFrame : IDisposable
{
    private readonly VsOutput _output;
    private readonly IntPtr _frame;

    /// <summary>
    /// Gets the zero-based frame index.
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Occurs when an asynchronous frame request is submitted.
    /// </summary>
    public static event EventHandler<int>? Requested;

    /// <summary>
    /// Occurs when a native frame reference is wrapped.
    /// </summary>
    public static event EventHandler<VsFrame>? Allocated;

    /// <summary>
    /// Occurs after a native frame reference is released.
    /// </summary>
    public static event EventHandler<VsFrame>? Deallocated;

    internal static void RaiseRequested(int index) => Requested?.Invoke(null, index);

    internal VsFrame(VsOutput output, IntPtr frame, int index)
    {
        _output = output;
        _frame = frame;
        Index = index;
        Allocated?.Invoke(this, this);
    }

    /// <summary>
    /// Releases the native frame reference; its planes must no longer be accessed.
    /// </summary>
    public void Dispose()
    {
        _output.Api.FreeFrame(_frame);
        Deallocated?.Invoke(this, this);
    }

    /// <summary>
    /// Returns a view of the specified zero-based plane, valid while this frame is alive.
    /// </summary>
    public VsPlane GetPlane(int plane) => new(_output, _frame, plane);
}
