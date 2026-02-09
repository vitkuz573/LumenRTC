namespace LumenRTC;

/// <summary>
/// Wraps the native audio device module for device selection and volume.
/// </summary>
public sealed partial class AudioDevice : SafeHandle
{
    private const int MaxDeviceNameSize = 128;
    private const int MaxGuidSize = 128;

    internal AudioDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public short PlayoutDevices() => NativeMethods.lrtc_audio_device_playout_devices(handle);

    public short RecordingDevices() => NativeMethods.lrtc_audio_device_recording_devices(handle);

    public AudioDeviceInfo GetPlayoutDeviceName(ushort index)
    {
        return GetDeviceName(isPlayout: true, index);
    }

    public AudioDeviceInfo GetRecordingDeviceName(ushort index)
    {
        return GetDeviceName(isPlayout: false, index);
    }

    private AudioDeviceInfo GetDeviceName(bool isPlayout, ushort index)
    {
        var nameBuf = Marshal.AllocHGlobal(MaxDeviceNameSize);
        var guidBuf = Marshal.AllocHGlobal(MaxGuidSize);
        try
        {
            int result = isPlayout
                ? NativeMethods.lrtc_audio_device_playout_device_name(handle, index, nameBuf, MaxDeviceNameSize, guidBuf, MaxGuidSize)
                : NativeMethods.lrtc_audio_device_recording_device_name(handle, index, nameBuf, MaxDeviceNameSize, guidBuf, MaxGuidSize);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to query audio device name.");
            }
            return new AudioDeviceInfo(Utf8String.Read(nameBuf), Utf8String.Read(guidBuf));
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
            Marshal.FreeHGlobal(guidBuf);
        }
    }

    public void SetPlayoutDevice(ushort index)
    {
        var result = NativeMethods.lrtc_audio_device_set_playout_device(handle, index);
        if (result != 0) throw new InvalidOperationException("Failed to set playout device.");
    }

    public void SetRecordingDevice(ushort index)
    {
        var result = NativeMethods.lrtc_audio_device_set_recording_device(handle, index);
        if (result != 0) throw new InvalidOperationException("Failed to set recording device.");
    }

    public void SetMicrophoneVolume(uint volume)
    {
        var result = NativeMethods.lrtc_audio_device_set_microphone_volume(handle, volume);
        if (result != 0) throw new InvalidOperationException("Failed to set microphone volume.");
    }

    public uint GetMicrophoneVolume()
    {
        var result = NativeMethods.lrtc_audio_device_microphone_volume(handle, out var volume);
        if (result != 0) throw new InvalidOperationException("Failed to get microphone volume.");
        return volume;
    }

    public void SetSpeakerVolume(uint volume)
    {
        var result = NativeMethods.lrtc_audio_device_set_speaker_volume(handle, volume);
        if (result != 0) throw new InvalidOperationException("Failed to set speaker volume.");
    }

    public uint GetSpeakerVolume()
    {
        var result = NativeMethods.lrtc_audio_device_speaker_volume(handle, out var volume);
        if (result != 0) throw new InvalidOperationException("Failed to get speaker volume.");
        return volume;
    }
}

public readonly record struct VideoDeviceInfo(string Name, string UniqueId);
