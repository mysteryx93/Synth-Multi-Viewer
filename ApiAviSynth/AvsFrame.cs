namespace HanumanInstitute.ApiAviSynth;

/// <summary>
/// Owns an AviSynth frame reference.
/// </summary>
public sealed class AvsFrame : IDisposable
{
    private readonly AvsNative _native;
    private IntPtr _frame;

    internal AvsFrame(AvsNative native, IntPtr frame)
    {
        _native = native;
        _frame = frame;
    }

    /// <summary>
    /// Gets a read-only image plane.
    /// </summary>
    public AvsPlane GetPlane(int plane)
    {
        ObjectDisposedException.ThrowIf(_frame == IntPtr.Zero, this);
        return new AvsPlane(_native.GetReadPtr(_frame, plane), _native.GetPitch(_frame, plane),
            _native.GetRowSize(_frame, plane), _native.GetHeight(_frame, plane));
    }

    /// <summary>
    /// Releases the native frame reference.
    /// </summary>
    public void Dispose()
    {
        if (_frame != IntPtr.Zero)
        {
            _native.ReleaseFrame(_frame);
            _frame = IntPtr.Zero;
        }
    }
}
