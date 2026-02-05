namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint lrtc_video_device_number_of_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_device_get_device_name(
        IntPtr device,
        uint index,
        IntPtr name,
        uint nameLen,
        IntPtr uniqueId,
        uint uniqueIdLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_device_create_capturer(
        IntPtr device,
        IntPtr name,
        uint index,
        nuint width,
        nuint height,
        nuint targetFps);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_device_release(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_video_capturer_start(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool lrtc_video_capturer_capture_started(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_capturer_stop(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_capturer_release(IntPtr capturer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_source_release(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_sink_create(
        ref LrtcVideoSinkCallbacks callbacks,
        IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_sink_release(IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_add_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_remove_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_track_get_id(
        IntPtr track,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_track_get_state(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_track_get_enabled(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_track_set_enabled(IntPtr track, int enabled);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_track_release(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_width(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_height(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_y(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_u(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_stride_v(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_y(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_u(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_data_v(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_copy_i420(
        IntPtr frame,
        IntPtr dstY,
        int dstStrideY,
        IntPtr dstU,
        int dstStrideU,
        IntPtr dstV,
        int dstStrideV);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_video_frame_to_argb(
        IntPtr frame,
        IntPtr dstArgb,
        int dstStrideArgb,
        int destWidth,
        int destHeight,
        int format);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_video_frame_retain(IntPtr frame);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_video_frame_release(IntPtr frame);
}
