using System;
using System.Threading;
using LumenRTC;

namespace LumenRTC.Rendering.Sdl;

public sealed class SdlVideoRenderer : IDisposable
{
    private readonly object _sync = new();
    private readonly SdlContext _context;
    private readonly string _title;
    private readonly int _initialWidth;
    private readonly int _initialHeight;
    private readonly uint _pixelFormat;
    private Thread? _thread;
    private bool _stopRequested;
    private bool _running;

    private IntPtr _window;
    private IntPtr _renderer;
    private IntPtr _texture;
    private int _textureWidth;
    private int _textureHeight;

    private byte[]? _buffer;
    private int _bufferWidth;
    private int _bufferHeight;
    private int _stride;
    private bool _dirty;

    public SdlVideoRenderer(string title, int width = 1280, int height = 720)
    {
        _context = SdlContext.Acquire();
        _title = string.IsNullOrWhiteSpace(title) ? "LumenRTC" : title;
        _initialWidth = Math.Max(1, width);
        _initialHeight = Math.Max(1, height);
        _pixelFormat = CreateArgbPixelFormat();
        Sink = new VideoSink(new VideoSinkCallbacks
        {
            OnFrame = OnFrame,
        });
    }

    public VideoSink Sink { get; }

    public bool IsRunning => _running;

    public void Start()
    {
        if (_thread != null)
        {
            throw new InvalidOperationException("Renderer already started.");
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "LumenRTC.SdlVideoRenderer",
        };
        _thread.Start();
    }

    public void Stop()
    {
        _stopRequested = true;
        if (_thread != null && Thread.CurrentThread != _thread)
        {
            _thread.Join();
            _thread = null;
        }
    }

    public void Run()
    {
        if (_running)
        {
            throw new InvalidOperationException("Renderer already running.");
        }

        _running = true;
        _stopRequested = false;

        try
        {
            InitializeWindow();
            Loop();
        }
        finally
        {
            ShutdownWindow();
            _running = false;
        }
    }

    private void InitializeWindow()
    {
        using var title = new Utf8String(_title);
        _window = SdlNative.SDL_CreateWindow(
            title.Pointer,
            unchecked((int)SdlNative.SDL_WINDOWPOS_CENTERED),
            unchecked((int)SdlNative.SDL_WINDOWPOS_CENTERED),
            _initialWidth,
            _initialHeight,
            SdlNative.SDL_WINDOW_SHOWN | SdlNative.SDL_WINDOW_RESIZABLE);
        if (_window == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateWindow failed: {SdlNative.GetError()}");
        }

        _renderer = SdlNative.SDL_CreateRenderer(
            _window,
            -1,
            SdlNative.SDL_RENDERER_ACCELERATED | SdlNative.SDL_RENDERER_PRESENTVSYNC);
        if (_renderer == IntPtr.Zero)
        {
            _renderer = SdlNative.SDL_CreateRenderer(_window, -1, 0);
        }

        if (_renderer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateRenderer failed: {SdlNative.GetError()}");
        }

        SdlNative.SDL_SetRenderDrawColor(_renderer, 0, 0, 0, 255);
    }

    private void Loop()
    {
        while (!_stopRequested)
        {
            while (SdlNative.SDL_PollEvent(out var evt) != 0)
            {
                if (evt.type == SdlNative.SDL_QUIT)
                {
                    _stopRequested = true;
                }
            }

            RenderFrame();

            SdlNative.SDL_Delay(10);
        }
    }

    private void RenderFrame()
    {
        byte[]? buffer;
        int width;
        int height;
        int stride;

        lock (_sync)
        {
            if (!_dirty || _buffer == null)
            {
                return;
            }

            buffer = _buffer;
            width = _bufferWidth;
            height = _bufferHeight;
            stride = _stride;
            _dirty = false;
        }

        EnsureTexture(width, height);

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                SdlNative.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, stride);
            }
        }

        SdlNative.SDL_RenderClear(_renderer);
        SdlNative.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
        SdlNative.SDL_RenderPresent(_renderer);
    }

    private void EnsureTexture(int width, int height)
    {
        if (_texture != IntPtr.Zero && (width != _textureWidth || height != _textureHeight))
        {
            SdlNative.SDL_DestroyTexture(_texture);
            _texture = IntPtr.Zero;
        }

        if (_texture != IntPtr.Zero)
        {
            return;
        }

        _texture = SdlNative.SDL_CreateTexture(
            _renderer,
            _pixelFormat,
            SdlNative.SDL_TEXTUREACCESS_STREAMING,
            width,
            height);
        if (_texture == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SDL_CreateTexture failed: {SdlNative.GetError()}");
        }

        _textureWidth = width;
        _textureHeight = height;
        SdlNative.SDL_SetWindowSize(_window, width, height);
    }

    private void OnFrame(VideoFrame frame)
    {
        var width = frame.Width;
        var height = frame.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_buffer == null || width != _bufferWidth || height != _bufferHeight)
            {
                _bufferWidth = width;
                _bufferHeight = height;
                _stride = width * 4;
                _buffer = new byte[_stride * height];
            }

            frame.CopyToArgb(_buffer, _stride, _bufferWidth, _bufferHeight, VideoFrameFormat.Argb);
            _dirty = true;
        }
    }

    private void ShutdownWindow()
    {
        if (_texture != IntPtr.Zero)
        {
            SdlNative.SDL_DestroyTexture(_texture);
            _texture = IntPtr.Zero;
        }

        if (_renderer != IntPtr.Zero)
        {
            SdlNative.SDL_DestroyRenderer(_renderer);
            _renderer = IntPtr.Zero;
        }

        if (_window != IntPtr.Zero)
        {
            SdlNative.SDL_DestroyWindow(_window);
            _window = IntPtr.Zero;
        }
    }

    private static uint CreateArgbPixelFormat()
    {
        const uint rMask = 0x00FF0000;
        const uint gMask = 0x0000FF00;
        const uint bMask = 0x000000FF;
        const uint aMask = 0xFF000000;
        var format = SdlNative.SDL_MasksToPixelFormatEnum(32, rMask, gMask, bMask, aMask);
        if (format == 0)
        {
            throw new InvalidOperationException("SDL does not support ARGB8888 pixel format.");
        }
        return format;
    }

    public void Dispose()
    {
        Stop();
        Sink.Dispose();
        _context.Dispose();
    }
}
