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
    public int StrideY => NativeMethods.lrtc_video_frame_stride_y(handle);
    public int StrideU => NativeMethods.lrtc_video_frame_stride_u(handle);
    public int StrideV => NativeMethods.lrtc_video_frame_stride_v(handle);
    public IntPtr DataY => NativeMethods.lrtc_video_frame_data_y(handle);
    public IntPtr DataU => NativeMethods.lrtc_video_frame_data_u(handle);
    public IntPtr DataV => NativeMethods.lrtc_video_frame_data_v(handle);

    public void CopyCurrentI420PlanesTo(Span<byte> yPlane, Span<byte> uPlane, Span<byte> vPlane)
    {
        var width = Width;
        var height = Height;

        var yRowWidth = width;
        var uvRowWidth = (width + 1) / 2;
        var yRows = height;
        var uvRows = (height + 1) / 2;

        var requiredY = checked(yRowWidth * yRows);
        var requiredUV = checked(uvRowWidth * uvRows);

        if (yPlane.Length < requiredY)
        {
            throw new ArgumentException(
                $"Destination plane '{nameof(yPlane)}' is too small. Expected at least {requiredY} bytes, got {yPlane.Length}.",
                nameof(yPlane));
        }

        if (uPlane.Length < requiredUV)
        {
            throw new ArgumentException(
                $"Destination plane '{nameof(uPlane)}' is too small. Expected at least {requiredUV} bytes, got {uPlane.Length}.",
                nameof(uPlane));
        }

        if (vPlane.Length < requiredUV)
        {
            throw new ArgumentException(
                $"Destination plane '{nameof(vPlane)}' is too small. Expected at least {requiredUV} bytes, got {vPlane.Length}.",
                nameof(vPlane));
        }

        // Delegate to the native copy path (libyuv SIMD) with packed strides
        // instead of an 8-call P/Invoke sequence followed by a managed row-by-row loop.
        CopyToI420(yPlane, yRowWidth, uPlane, uvRowWidth, vPlane, uvRowWidth);
    }

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
