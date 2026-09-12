namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Provides access to one image plane while its owning frame remains alive.
/// </summary>
public class VsPlane
{
    private readonly VsOutput _output;
    private readonly IntPtr _frame;
    private int? _stride;
    private IntPtr? _ptr;
    private int? _width;
    private int? _height;

    /// <summary>
    /// Gets the zero-based plane index.
    /// </summary>
    public int Plane { get; }

    internal VsPlane(VsOutput output, IntPtr frame, int plane)
    {
        _output = output;
        _frame = frame;
        Plane = plane;
    }

    /// <summary>
    /// Gets the byte distance between adjacent image rows.
    /// </summary>
    public int Stride => _stride ??= checked((int)_output.Api.GetStride(_frame, Plane));

    /// <summary>
    /// Gets the read-only pixel address, valid until the owning frame is released.
    /// </summary>
    public IntPtr Ptr => _ptr ??= _output.Api.GetReadPtr(_frame, Plane);

    /// <summary>
    /// Gets the plane width in pixels.
    /// </summary>
    public int Width => _width ??= _output.Api.GetFrameWidth(_frame, Plane);

    /// <summary>
    /// Gets the plane height in pixels.
    /// </summary>
    public int Height => _height ??= _output.Api.GetFrameHeight(_frame, Plane);

    /// <summary>
    /// Gets the writable pixel address; the caller must own a writable frame.
    /// </summary>
    public IntPtr GetWritePtr() => _output.Api.GetWritePtr(_frame, Plane);
}
