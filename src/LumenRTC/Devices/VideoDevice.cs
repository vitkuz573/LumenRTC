namespace LumenRTC;

public sealed class VideoDevice : SafeHandle
{
    private const int MaxNameSize = 256;
    private const int MaxUniqueIdSize = 256;

    internal VideoDevice(IntPtr handle) : base(IntPtr.Zero, true)
    {
        SetHandle(handle);
    }

    public uint NumberOfDevices() => NativeMethods.lrtc_video_device_number_of_devices(handle);

    public VideoDeviceInfo GetDeviceName(uint index)
    {
        var nameBuf = Marshal.AllocHGlobal(MaxNameSize);
        var idBuf = Marshal.AllocHGlobal(MaxUniqueIdSize);
        try
        {
            var result = NativeMethods.lrtc_video_device_get_device_name(handle, index, nameBuf, MaxNameSize, idBuf, MaxUniqueIdSize);
            if (result != 0)
            {
                throw new InvalidOperationException("Failed to query video device name.");
            }
            return new VideoDeviceInfo(Utf8String.Read(nameBuf), Utf8String.Read(idBuf));
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuf);
            Marshal.FreeHGlobal(idBuf);
        }
    }

    public VideoCapturer CreateCapturer(string name, uint index, uint width, uint height, uint fps)
    {
        using var nameUtf8 = new Utf8String(name);
        var capturer = NativeMethods.lrtc_video_device_create_capturer(handle, nameUtf8.Pointer, index, width, height, fps);
        if (capturer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create video capturer.");
        }
        return new VideoCapturer(capturer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_video_device_release(handle);
        return true;
    }
}
