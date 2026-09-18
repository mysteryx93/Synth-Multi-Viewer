using System.Runtime.InteropServices;

namespace HanumanInstitute.ApiVapourSynth;

/// <summary>
/// Owns a script output node and queues frames for ordered display.
/// </summary>
public class VsOutput : IDisposable
{
    private readonly IntPtr _scriptPtr;
    private readonly IntPtr _nodePtr;
    private readonly VsScriptApi _scriptApi;
    private readonly List<VsFrameStatus> _queue = [];
    private readonly SemaphoreSlim _displaySemaphore = new(1, 1);
    private readonly VsFrameDoneCallback _getFrameAsyncCallback;
    private readonly IntPtr _getFrameAsyncCallbackPtr;
    private bool _isClearingQueue;
    private DateTime _displayTime = DateTime.MinValue;
    private double _maxFps;
    private TimeSpan _maxFpsSpan;
    private Action? _clearQueueCallback;
    private int _activeCallbacks;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate void VsFrameDoneCallback(IntPtr userData, IntPtr frameRef, int n, IntPtr nodeRef, IntPtr errorMsg);

    /// <summary>
    /// Occurs when a frame is done processing.
    /// </summary>
    public event EventHandler<VsFrameStatus>? FrameDone;

    /// <summary>
    /// Occurs when the frame in front of the queue is ready for display.
    /// </summary>
    public event EventHandler<VsFrameStatus>? FrameReady;

    /// <summary>
    /// Gets the native function table used by this output.
    /// </summary>
    internal VsCoreApi Api { get; }

    internal VsOutput(VsScriptApi scriptApi, IntPtr scriptPtr, int nodeIndex)
    {
        _scriptApi = scriptApi;
        _getFrameAsyncCallback = GetFrameAsyncCallback;
        _getFrameAsyncCallbackPtr = Marshal.GetFunctionPointerForDelegate(_getFrameAsyncCallback);
        _scriptPtr = scriptPtr;
        _nodePtr = scriptApi.GetOutputNode(scriptPtr, nodeIndex);
        if (_nodePtr == IntPtr.Zero)
        {
            throw new VsException("The script did not set the requested video output.");
        }

        Api = VsCoreApi.Load(scriptApi.CoreApi);
    }

    /// <summary>
    /// Releases the output node after outstanding frame requests have completed.
    /// </summary>
    public void Dispose()
    {
        ClearQueue();
        WaitForIdle();
        Api.FreeNode(_nodePtr);
    }

    /// <summary>
    /// Gets or sets the display rate limit; zero disables throttling.
    /// </summary>
    public double MaxFps
    {
        get => _maxFps;
        set
        {
            _maxFps = value;
            _maxFpsSpan = _maxFps > 0 ? TimeSpan.FromSeconds(1 / _maxFps) : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Gets the video format, dimensions, frame count, and frame rate.
    /// </summary>
    public VsVideoInfo VideoInfo
    {
        get
        {
            var pointer = Api.GetVideoInfo(_nodePtr);
            if (pointer == IntPtr.Zero)
            {
                throw new("VapourSynth failed to get GetVideoInfo.");
            }

            return Marshal.PtrToStructure<VsVideoInfo>(pointer)
                ?? throw new("VapourSynth failed to get GetVideoInfo.");
        }
    }

    /// <summary>
    /// Retrieves a frame synchronously; the caller must dispose the returned frame.
    /// </summary>
    public VsFrame GetFrame(int index)
    {
        const int bufSize = 1024;
        var errorPtr = Marshal.AllocCoTaskMem(bufSize);
        try
        {
            var result = Api.GetFrame(index, _nodePtr, errorPtr, bufSize);
            if (result == IntPtr.Zero)
            {
                throw new VsException(Utf8Ptr.FromUtf8Ptr(errorPtr, bufSize) ?? "VapourSynth could not return the requested frame.");
            }

            return new(this, result, index);
        }
        finally
        {
            Marshal.FreeCoTaskMem(errorPtr);
        }
    }

    /// <summary>
    /// Gets the native core associated with the script.
    /// </summary>
    public IntPtr Core => _scriptApi.GetCore(_scriptPtr);

    /// <summary>
    /// Sets the core worker count and returns the count reported by VapourSynth.
    /// </summary>
    public int SetThreadCount(int threads) => Api.SetThreadCount(threads, Core);

    /// <summary>
    /// Queues a frame request whose result is delivered through FrameDone and FrameReady.
    /// </summary>
    public void GetFrameAsync(int index)
    {
        lock (_queue)
        {
            _queue.Add(new(index));
        }

        Api.GetFrameAsync(index, _nodePtr, _getFrameAsyncCallbackPtr, IntPtr.Zero);
        VsFrame.RaiseRequested(index);
    }

    private void GetFrameAsyncCallback(IntPtr userData, IntPtr frameRef, int n, IntPtr nodeRef, IntPtr errorMsg)
    {
        Interlocked.Increment(ref _activeCallbacks);
        try
        {
            HandleFrameCallback(frameRef, n, errorMsg);
        }
        finally
        {
            Interlocked.Decrement(ref _activeCallbacks);
        }
    }

    private void HandleFrameCallback(IntPtr frameRef, int n, IntPtr errorMsg)
    {
        var newFrame = new VsFrame(this, frameRef, n);
        var callbackList = new List<VsFrameStatus>();
        VsFrameStatus? found = null;
        lock (_queue)
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Index == n && _queue[i].Frame == null)
                {
                    _queue[i].Frame = newFrame;
                    if (_queue[i].State == VsFrameState.Requested)
                    {
                        _queue[i].State = VsFrameState.Completed;
                    }

                    _queue[i].Error = Utf8Ptr.FromUtf8Ptr(errorMsg);
                    found = _queue[i];
                    break;
                }
            }

            if (found == null)
            {
                throw new InvalidOperationException("GetFrameAsyncCallback received a frame not found in the processing queue.");
            }

            if (found.State == VsFrameState.Cancelled)
            {
                found.Frame!.Dispose();
                _queue.Remove(found);
                return;
            }

            while (_queue.Count > 0 && _queue[0].State == VsFrameState.Completed)
            {
                callbackList.Add(_queue[0]);
                _queue.RemoveAt(0);
            }
        }

        FrameDone?.Invoke(this, found);
        if (callbackList.Count == 0)
        {
            return;
        }

        _displaySemaphore.Wait();
        try
        {
            _isClearingQueue = false;
            foreach (var item in callbackList)
            {
                if (_maxFps > 0)
                {
                    var frameDelay = DateTime.Now - _displayTime;
                    if (frameDelay < _maxFpsSpan)
                    {
                        Thread.Sleep(_maxFpsSpan - frameDelay);
                    }

                    _displayTime = DateTime.Now;
                }

                if (_isClearingQueue)
                {
                    item.Frame!.Dispose();
                }
                else
                {
                    FrameReady?.Invoke(this, item);
                    item.Frame!.Dispose();
                    if (MaxFps > 0)
                    {
                        Thread.Yield();
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            }
        }
        finally
        {
            _displaySemaphore.Release();
        }
    }

    /// <summary>
    /// Clears the processing queue.
    /// </summary>
    /// <returns>The amount of frames cleared from the queue.</returns>
    public int ClearQueue() => ClearQueue(null);

    /// <summary>
    /// Clears the processing queue.
    /// </summary>
    /// <param name="callback">Calls this method after all cancelled frames are done processing.</param>
    /// <returns>The amount of frames cleared from the queue.</returns>
    public int ClearQueue(Action? callback)
    {
        var cleared = 0;
        lock (_queue)
        {
            _isClearingQueue = true;
            for (var i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i];
                if (item.State == VsFrameState.Completed)
                {
                    item.Frame!.Dispose();
                    item.Frame = null;
                    cleared++;
                    _queue.RemoveAt(i--);
                }
                else if (item.State == VsFrameState.Requested)
                {
                    item.State = VsFrameState.Cancelled;
                    cleared++;
                }
            }

            if (callback != null)
            {
                _clearQueueCallback = callback;
            }
        }

        if (callback != null)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                WaitForIdle();
                var done = Interlocked.Exchange(ref _clearQueueCallback, null);
                done?.Invoke();
            });
        }

        return cleared;
    }

    /// <summary>
    /// Returns the amount of uncompleted frames in the processing queue.
    /// </summary>
    public int GetQueueLength(VsFrameState state)
    {
        lock (_queue)
        {
            var result = 0;
            for (var i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].State == state)
                {
                    result++;
                }
            }

            return result;
        }
    }

    private void WaitForIdle()
    {
        while (true)
        {
            lock (_queue)
            {
                if (_queue.Count == 0 && Volatile.Read(ref _activeCallbacks) == 0)
                {
                    break;
                }
            }

            Thread.Sleep(1);
        }

        // Let the native getFrameAsync callback return before FreeNode.
        Thread.Sleep(1);
    }
}
