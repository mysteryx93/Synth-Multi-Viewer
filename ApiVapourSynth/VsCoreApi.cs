using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

internal sealed class VsCoreApi
{
    private readonly FreeNodeDelegate _freeNode;
    private readonly FreeFrameDelegate _freeFrame;
    private readonly GetVideoInfoDelegate _getVideoInfo;
    private readonly GetFrameDelegate _getFrame;
    private readonly GetFrameAsyncDelegate _getFrameAsync;
    private readonly GetStrideDelegate _getStride;
    private readonly GetReadPtrDelegate _getReadPtr;
    private readonly GetWritePtrDelegate _getWritePtr;
    private readonly GetFrameDimensionDelegate _getFrameWidth;
    private readonly GetFrameDimensionDelegate _getFrameHeight;
    private readonly SetThreadCountDelegate _setThreadCount;

    private VsCoreApi(VsCoreApiTable table)
    {
        _freeNode = Marshal.GetDelegateForFunctionPointer<FreeNodeDelegate>(table.FreeNode);
        _freeFrame = Marshal.GetDelegateForFunctionPointer<FreeFrameDelegate>(table.FreeFrame);
        _getVideoInfo = Marshal.GetDelegateForFunctionPointer<GetVideoInfoDelegate>(table.GetVideoInfo);
        _getFrame = Marshal.GetDelegateForFunctionPointer<GetFrameDelegate>(table.GetFrame);
        _getFrameAsync = Marshal.GetDelegateForFunctionPointer<GetFrameAsyncDelegate>(table.GetFrameAsync);
        _getStride = Marshal.GetDelegateForFunctionPointer<GetStrideDelegate>(table.GetStride);
        _getReadPtr = Marshal.GetDelegateForFunctionPointer<GetReadPtrDelegate>(table.GetReadPtr);
        _getWritePtr = Marshal.GetDelegateForFunctionPointer<GetWritePtrDelegate>(table.GetWritePtr);
        _getFrameWidth = Marshal.GetDelegateForFunctionPointer<GetFrameDimensionDelegate>(table.GetFrameWidth);
        _getFrameHeight = Marshal.GetDelegateForFunctionPointer<GetFrameDimensionDelegate>(table.GetFrameHeight);
        _setThreadCount = Marshal.GetDelegateForFunctionPointer<SetThreadCountDelegate>(table.SetThreadCount);
    }

    public void FreeNode(IntPtr node) => _freeNode(node);
    public void FreeFrame(IntPtr frame) => _freeFrame(frame);
    public IntPtr GetVideoInfo(IntPtr node) => _getVideoInfo(node);
    public IntPtr GetFrame(int index, IntPtr node, IntPtr error, int errorSize) => _getFrame(index, node, error, errorSize);
    public void GetFrameAsync(int index, IntPtr node, IntPtr callback, IntPtr userData) => _getFrameAsync(index, node, callback, userData);
    public nint GetStride(IntPtr frame, int plane) => _getStride(frame, plane);
    public IntPtr GetReadPtr(IntPtr frame, int plane) => _getReadPtr(frame, plane);
    public IntPtr GetWritePtr(IntPtr frame, int plane) => _getWritePtr(frame, plane);
    public int GetFrameWidth(IntPtr frame, int plane) => _getFrameWidth(frame, plane);
    public int GetFrameHeight(IntPtr frame, int plane) => _getFrameHeight(frame, plane);
    public int SetThreadCount(int threads, IntPtr core) => _setThreadCount(threads, core);

    public static VsCoreApi Load(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            throw new VsException("The loaded VSScript library does not expose the VapourSynth 4 core API.");
        }

        return new VsCoreApi(Marshal.PtrToStructure<VsCoreApiTable>(pointer));
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeNodeDelegate(IntPtr node);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void FreeFrameDelegate(IntPtr frame);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetVideoInfoDelegate(IntPtr node);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetFrameDelegate(int index, IntPtr node, IntPtr error, int errorSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void GetFrameAsyncDelegate(int index, IntPtr node, IntPtr callback, IntPtr userData);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint GetStrideDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetReadPtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr GetWritePtrDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int GetFrameDimensionDelegate(IntPtr frame, int plane);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int SetThreadCountDelegate(int threads, IntPtr core);
}
