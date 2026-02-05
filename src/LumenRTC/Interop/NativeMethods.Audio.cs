namespace LumenRTC.Interop;

internal static partial class NativeMethods
{
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short lrtc_audio_device_playout_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern short lrtc_audio_device_recording_devices(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_playout_device_name(
        IntPtr device,
        ushort index,
        IntPtr name,
        uint nameLen,
        IntPtr guid,
        uint guidLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_recording_device_name(
        IntPtr device,
        ushort index,
        IntPtr name,
        uint nameLen,
        IntPtr guid,
        uint guidLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_playout_device(IntPtr device, ushort index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_recording_device(IntPtr device, ushort index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_microphone_volume(IntPtr device, uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_microphone_volume(IntPtr device, out uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_set_speaker_volume(IntPtr device, uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_device_speaker_volume(IntPtr device, out uint volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_device_release(IntPtr device);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_source_capture_frame(
        IntPtr source,
        IntPtr audioData,
        int bitsPerSample,
        int sampleRate,
        nuint channels,
        nuint frames);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_source_release(IntPtr source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_set_volume(IntPtr track, double volume);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_track_get_id(
        IntPtr track,
        IntPtr buffer,
        uint bufferLen);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_track_get_state(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_track_get_enabled(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lrtc_audio_track_set_enabled(IntPtr track, int enabled);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_add_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_remove_sink(IntPtr track, IntPtr sink);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_track_release(IntPtr track);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr lrtc_audio_sink_create(ref LrtcAudioSinkCallbacks callbacks, IntPtr userData);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lrtc_audio_sink_release(IntPtr sink);
}
