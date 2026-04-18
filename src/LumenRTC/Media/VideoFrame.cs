using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace LumenRTC;

/// <summary>
/// Video frame handle provided by sinks.
/// <para>
/// <b>Pooled object</b> — do NOT hold a reference past the <see cref="Dispose"/> call.
/// The instance is returned to an internal pool and will be reused for the next frame.
/// Call <see cref="Retain"/> if you need to keep a frame alive longer than the callback.
/// </para>
/// </summary>
public sealed class VideoFrame : IDisposable
{
    // -------------------------------------------------------------------------
    // Pool
    // -------------------------------------------------------------------------
    private static readonly ConcurrentBag<VideoFrame> s_pool = new();
    private const int PoolMaxSize = 64; // guard against excessive pooling
    private static int s_poolCount;

    internal static VideoFrame Rent(IntPtr handle)
    {
        if (s_pool.TryTake(out var frame))
        {
            System.Threading.Interlocked.Decrement(ref s_poolCount);
            frame._handle = handle;
            frame._disposed = false;
            return frame;
        }
        return new VideoFrame(handle);
    }

    private static void Return(VideoFrame frame)
    {
        // Release the native handle first, then recycle the managed wrapper.
        var h = frame._handle;
        frame._handle = IntPtr.Zero;
        if (h != IntPtr.Zero)
            NativeMethods.lrtc_video_frame_release(h);

        if (System.Threading.Interlocked.Increment(ref s_poolCount) <= PoolMaxSize)
            s_pool.Add(frame);
        else
            System.Threading.Interlocked.Decrement(ref s_poolCount);
    }

    // -------------------------------------------------------------------------
    // Instance
    // -------------------------------------------------------------------------
    private IntPtr _handle;
    private bool _disposed;

    private VideoFrame(IntPtr handle)
    {
        _handle = handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr ValidHandle()
    {
        if (_disposed || _handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(VideoFrame));
        return _handle;
    }

    public int Width  => NativeMethods.lrtc_video_frame_width(ValidHandle());
    public int Height => NativeMethods.lrtc_video_frame_height(ValidHandle());
    public int StrideY => NativeMethods.lrtc_video_frame_stride_y(ValidHandle());
    public int StrideU => NativeMethods.lrtc_video_frame_stride_u(ValidHandle());
    public int StrideV => NativeMethods.lrtc_video_frame_stride_v(ValidHandle());
    public IntPtr DataY => NativeMethods.lrtc_video_frame_data_y(ValidHandle());
    public IntPtr DataU => NativeMethods.lrtc_video_frame_data_u(ValidHandle());
    public IntPtr DataV => NativeMethods.lrtc_video_frame_data_v(ValidHandle());

    public void CopyCurrentI420PlanesTo(Span<byte> yPlane, Span<byte> uPlane, Span<byte> vPlane)
    {
        var width  = Width;
        var height = Height;

        var yRowWidth  = width;
        var uvRowWidth = (width + 1) / 2;
        var yRows      = height;
        var uvRows     = (height + 1) / 2;

        var requiredY  = checked(yRowWidth * yRows);
        var requiredUV = checked(uvRowWidth * uvRows);

        if (yPlane.Length < requiredY)
            throw new ArgumentException(
                $"Destination plane '{nameof(yPlane)}' is too small. Expected at least {requiredY} bytes, got {yPlane.Length}.",
                nameof(yPlane));
        if (uPlane.Length < requiredUV)
            throw new ArgumentException(
                $"Destination plane '{nameof(uPlane)}' is too small. Expected at least {requiredUV} bytes, got {uPlane.Length}.",
                nameof(uPlane));
        if (vPlane.Length < requiredUV)
            throw new ArgumentException(
                $"Destination plane '{nameof(vPlane)}' is too small. Expected at least {requiredUV} bytes, got {vPlane.Length}.",
                nameof(vPlane));

        CopyToI420(yPlane, yRowWidth, uPlane, uvRowWidth, vPlane, uvRowWidth);
    }

    public void CopyToI420(Span<byte> y, int strideY, Span<byte> u, int strideU, Span<byte> v, int strideV)
    {
        var h = ValidHandle();
        unsafe
        {
            fixed (byte* yPtr = y)
            fixed (byte* uPtr = u)
            fixed (byte* vPtr = v)
            {
                NativeMethods.lrtc_video_frame_copy_i420(h, (IntPtr)yPtr, strideY, (IntPtr)uPtr, strideU, (IntPtr)vPtr, strideV);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, int format)
    {
        var h = ValidHandle();
        unsafe
        {
            fixed (byte* argbPtr = argb)
            {
                NativeMethods.lrtc_video_frame_to_argb(h, (IntPtr)argbPtr, strideArgb, width, height, format);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, VideoFrameFormat format)
        => CopyToArgb(argb, strideArgb, width, height, (int)format);

    /// <summary>
    /// Retains a new independent frame handle that outlives the current callback scope.
    /// The caller owns the returned frame and must call <see cref="Dispose"/> on it.
    /// </summary>
    public VideoFrame Retain()
    {
        var retained = NativeMethods.lrtc_video_frame_retain(ValidHandle());
        if (retained == IntPtr.Zero)
            throw new InvalidOperationException("Failed to retain video frame.");
        // Retained frames are NOT from the pool — they are one-shot.
        return new VideoFrame(retained);
    }

    /// <summary>Returns this frame to the pool (or releases its native handle if the pool is full).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Return(this);
    }
}
