namespace LumenRTC;

public sealed class MediaConstraints : SafeHandle
{
    private MediaConstraints() : base(IntPtr.Zero, true) { }

    public static MediaConstraints Create()
    {
        var handle = NativeMethods.lrtc_media_constraints_create();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create media constraints.");
        }
        var constraints = new MediaConstraints();
        constraints.SetHandle(handle);
        return constraints;
    }

    public void AddMandatory(string key, string value)
    {
        using var keyUtf8 = new Utf8String(key);
        using var valueUtf8 = new Utf8String(value);
        NativeMethods.lrtc_media_constraints_add_mandatory(handle, keyUtf8.Pointer, valueUtf8.Pointer);
    }

    public void AddOptional(string key, string value)
    {
        using var keyUtf8 = new Utf8String(key);
        using var valueUtf8 = new Utf8String(value);
        NativeMethods.lrtc_media_constraints_add_optional(handle, keyUtf8.Pointer, valueUtf8.Pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        NativeMethods.lrtc_media_constraints_release(handle);
        return true;
    }
}
