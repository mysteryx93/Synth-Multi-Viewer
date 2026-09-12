using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

[StructLayout(LayoutKind.Sequential)]
internal struct VsCoreApiTable
{
    public IntPtr CreateVideoFilter;
    public IntPtr CreateVideoFilter2;
    public IntPtr CreateAudioFilter;
    public IntPtr CreateAudioFilter2;
    public IntPtr SetLinearFilter;
    public IntPtr SetCacheMode;
    public IntPtr SetCacheOptions;
    public IntPtr FreeNode;
    public IntPtr AddNodeRef;
    public IntPtr GetNodeType;
    public IntPtr GetVideoInfo;
    public IntPtr GetAudioInfo;
    public IntPtr NewVideoFrame;
    public IntPtr NewVideoFrame2;
    public IntPtr NewAudioFrame;
    public IntPtr NewAudioFrame2;
    public IntPtr FreeFrame;
    public IntPtr AddFrameRef;
    public IntPtr CopyFrame;
    public IntPtr GetFramePropertiesRo;
    public IntPtr GetFramePropertiesRw;
    public IntPtr GetStride;
    public IntPtr GetReadPtr;
    public IntPtr GetWritePtr;
    public IntPtr GetVideoFrameFormat;
    public IntPtr GetAudioFrameFormat;
    public IntPtr GetFrameType;
    public IntPtr GetFrameWidth;
    public IntPtr GetFrameHeight;
    public IntPtr GetFrameLength;
    public IntPtr GetVideoFormatName;
    public IntPtr GetAudioFormatName;
    public IntPtr QueryVideoFormat;
    public IntPtr QueryAudioFormat;
    public IntPtr QueryVideoFormatId;
    public IntPtr GetVideoFormatById;
    public IntPtr GetFrame;
    public IntPtr GetFrameAsync;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 62)]
    public IntPtr[] BeforeSetThreadCount;

    public IntPtr SetThreadCount;
}
