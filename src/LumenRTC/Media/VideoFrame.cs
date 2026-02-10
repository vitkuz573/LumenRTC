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

        CopyPlane(DataY, StrideY, yPlane, yRowWidth, yRows, nameof(yPlane));
        CopyPlane(DataU, StrideU, uPlane, uvRowWidth, uvRows, nameof(uPlane));
        CopyPlane(DataV, StrideV, vPlane, uvRowWidth, uvRows, nameof(vPlane));
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

    private static void CopyPlane(
        IntPtr source,
        int sourceStride,
        Span<byte> destination,
        int rowWidth,
        int rowCount,
        string destinationName)
    {
        if (source == IntPtr.Zero)
        {
            throw new InvalidOperationException("Video frame plane pointer is null.");
        }

        if (sourceStride < rowWidth)
        {
            throw new InvalidOperationException(
                $"Plane stride ({sourceStride}) is smaller than row width ({rowWidth}).");
        }

        var requiredBytes = checked(rowWidth * rowCount);
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"Destination plane '{destinationName}' is too small. Expected at least {requiredBytes} bytes, got {destination.Length}.",
                destinationName);
        }

        unsafe
        {
            var src = (byte*)source.ToPointer();
            for (var row = 0; row < rowCount; row++)
            {
                var srcRow = new ReadOnlySpan<byte>(src + row * sourceStride, rowWidth);
                srcRow.CopyTo(destination.Slice(row * rowWidth, rowWidth));
            }
        }
    }
}
