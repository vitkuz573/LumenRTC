namespace LumenRTC;

public sealed class VideoSinkCallbacks
{
    public Action<VideoFrame>? OnFrame;
    private LrtcVideoFrameCb? _frameCb;

    internal LrtcVideoSinkCallbacks BuildNative()
    {
        _frameCb = (ud, framePtr) =>
        {
            if (framePtr == IntPtr.Zero) return;
            using var frame = new VideoFrame(framePtr);
            OnFrame?.Invoke(frame);
        };

        return new LrtcVideoSinkCallbacks
        {
            on_frame = _frameCb,
        };
    }
}
