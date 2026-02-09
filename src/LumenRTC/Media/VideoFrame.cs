namespace LumenRTC;

/// <summary>
/// Video frame handle provided by sinks.
/// </summary>
public sealed partial class VideoFrame : SafeHandle
{
    internal VideoFrame(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public int Width => NativeMethods.lrtc_video_frame_width(handle);
    public int Height => NativeMethods.lrtc_video_frame_height(handle);

    public void CopyToI420(Span<byte> y, int strideY, Span<byte> u, int strideU, Span<byte> v, int strideV)
    {
        unsafe
        {
            fixed (byte* yPtr = y)
            fixed (byte* uPtr = u)
            fixed (byte* vPtr = v)
            {
                NativeMethods.lrtc_video_frame_copy_i420(handle, (IntPtr)yPtr, strideY, (IntPtr)uPtr, strideU, (IntPtr)vPtr, strideV);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, int format)
    {
        unsafe
        {
            fixed (byte* argbPtr = argb)
            {
                NativeMethods.lrtc_video_frame_to_argb(handle, (IntPtr)argbPtr, strideArgb, width, height, format);
            }
        }
    }

    public void CopyToArgb(Span<byte> argb, int strideArgb, int width, int height, VideoFrameFormat format)
    {
        CopyToArgb(argb, strideArgb, width, height, (int)format);
    }

    public VideoFrame Retain()
    {
        var retained = NativeMethods.lrtc_video_frame_retain(handle);
        if (retained == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to retain video frame.");
        }
        return new VideoFrame(retained);
    }
}
