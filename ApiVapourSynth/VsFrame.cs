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

    /// <summary>
    /// Returns the frame property map as name/value pairs.
    /// </summary>
    public IReadOnlyList<(string Name, string Value)> GetProperties()
    {
        var api = _output.Api;
        var map = api.GetFramePropertiesRo(_frame);
        if (map == IntPtr.Zero)
        {
            return [];
        }

        var count = api.MapNumKeys(map);
        if (count <= 0)
        {
            return [];
        }

        var properties = new List<(string Name, string Value)>(count);
        for (var i = 0; i < count; i++)
        {
            var keyPtr = api.MapGetKey(map, i);
            var key = Utf8Ptr.FromUtf8Ptr(keyPtr);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            properties.Add((key, FormatValue(api, map, keyPtr)));
        }

        return properties;
    }

    private static string FormatValue(VsCoreApi api, IntPtr map, IntPtr key)
    {
        var type = api.MapGetType(map, key);
        var count = api.MapNumElements(map, key);
        if (count <= 0)
        {
            return "";
        }

        return type switch
        {
            1 => Join(count, i => api.MapGetInt(map, key, i, out _).ToString()),
            2 => Join(count, i => api.MapGetFloat(map, key, i, out _).ToString("G")),
            3 => Join(count, i => FormatData(api, map, key, i)),
            4 => Named(count, "function"),
            5 => Named(count, "video node"),
            6 => Named(count, "audio node"),
            7 => Named(count, "video frame"),
            8 => Named(count, "audio frame"),
            _ => "<unset>"
        };
    }

    private static string FormatData(VsCoreApi api, IntPtr map, IntPtr key, int index)
    {
        var pointer = api.MapGetData(map, key, index, out _);
        if (pointer == IntPtr.Zero)
        {
            return "";
        }

        var size = api.MapGetDataSize(map, key, index, out _);
        return size <= 0 ? "" : Utf8Ptr.FromUtf8Ptr(pointer, size) ?? "";
    }

    private static string Named(int count, string type) => count == 1 ? "<" + type + ">" : count + " " + type + "s";

    private static string Join(int count, Func<int, string> value)
    {
        if (count == 1)
        {
            return value(0);
        }

        var parts = new string[count];
        for (var i = 0; i < count; i++)
        {
            parts[i] = value(i);
        }

        return string.Join(", ", parts);
    }
}
