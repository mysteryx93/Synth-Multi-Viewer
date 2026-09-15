using Avalonia.Media.Imaging;

namespace HanumanInstitute.MediaSynthUI;

/// <summary>
/// Native script session owned by <see cref="SynthPlayerHost"/>.
/// </summary>
internal interface ISynthPlayback : IDisposable
{
    int Width { get; }
    int Height { get; }
    TimeSpan Duration { get; }
    ClipInfo? ClipInfo { get; }
    IReadOnlyList<FrameProperty> ReadFrameProperties(int index);
    void Present(int index);
    void Play();
    void Pause();
    void Seek(int index, bool playing);
    void Unload();
    void SetThreadCount(int threads);
    void ApplyLimitFps(bool limit);
}

/// <summary>
/// Host callbacks used by a native playback session.
/// </summary>
internal interface ISynthPlayerSink
{
    object Gate { get; }
    bool IsPlaying { get; }
    bool LimitFps { get; }
    bool IsDisposed { get; }
    bool IsErrorVisible { get; }
    WriteableBitmap? Bitmap { get; }
    int PositionRequested { get; set; }
    int ThreadCount { get; }
    void DisplayError(string message);
    void ShowBitmap();
    void SetPosition(int index, IReadOnlyList<FrameProperty> properties);
    void ClearVideo();
    void ContinueAfterStop();
}
