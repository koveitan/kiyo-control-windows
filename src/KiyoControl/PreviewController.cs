using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kiyo.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace KiyoControl;

/// <summary>
/// Live preview of the Kiyo so changes can be judged before saving. Uses Windows.Media.Capture with a
/// frame reader, so the camera stays shared through the Windows camera service.
/// </summary>
public sealed class PreviewController(Image target, AppSettings settings)
{
    public List<VideoMode> Modes { get; private set; } = [];
    public VideoMode? Mode { get; private set; }
    public string Status { get; private set; } = "";
    public bool Running => _reader is not null;
    /// <summary>True when another app holds the camera, so this preview can't choose the format.</summary>
    public bool Shared { get; private set; }

    /// <summary>Device path of the camera the settings control, so the preview shows the same one when several Kiyos are plugged in.</summary>
    public string? CameraId { get; set; }

    /// <summary>Raised on the UI thread when modes, status or running state change.</summary>
    public event Action? StateChanged;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private MediaFrameSource? _source;
    private WriteableBitmap? _bitmap;
    private byte[] _buffer = [];
    private int _width, _height;
    private int _pending;

    public async Task StartAsync()
    {
        await _gate.WaitAsync();
        try { await StartLockedAsync(VideoMode.Parse(settings.PreviewMode)); }
        finally { _gate.Release(); }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await StopLockedAsync();
            SetStatus("");
        }
        finally { _gate.Release(); }
    }

    /// <summary>Re-opens the camera, e.g. after a replug changed the formats it offers.</summary>
    public async Task RestartAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await StopLockedAsync();
            await StartLockedAsync(VideoMode.Parse(settings.PreviewMode));
        }
        finally { _gate.Release(); }
    }

    public async Task SelectAsync(VideoMode mode)
    {
        settings.PreviewMode = mode.Stored;
        settings.Save();
        await RestartAsync();
    }

    private async Task StartLockedAsync(VideoMode? wanted)
    {
        if (_reader is not null) return;
        SetStatus("Starting preview…");
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            static IEnumerable<string> Ids(MediaFrameSourceGroup g) => g.SourceInfos.Select(i => i.DeviceInformation?.Id ?? "");
            // The camera the settings control; otherwise the first supported Kiyo.
            var group = groups.FirstOrDefault(g => Ids(g).Any(id => CameraDiscovery.SameDevice(id, CameraId)))
                        ?? groups.FirstOrDefault(g => Ids(g).Any(id =>
                            KiyoDevice.All.Any(d => id.Contains(d.UsbIdFragment, StringComparison.OrdinalIgnoreCase))));
            if (group is null)
            {
                SetStatus("Camera not found");
                return;
            }

            // Exclusive control lets the preview pick its format; when another app has it, share read-only.
            _capture = await OpenAsync(group, MediaCaptureSharingMode.ExclusiveControl);
            Shared = _capture is null;
            _capture ??= await OpenAsync(group, MediaCaptureSharingMode.SharedReadOnly);
            if (_capture is null)
            {
                SetStatus("The camera is in use by another app that does not share it.");
                return;
            }

            _source = _capture.FrameSources.Values
                .Where(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                .OrderBy(s => s.Info.MediaStreamType == MediaStreamType.VideoPreview ? 0 : 1)
                .FirstOrDefault();
            if (_source is null)
            {
                SetStatus("The camera offers no color stream.");
                await StopLockedAsync();
                return;
            }

            var formats = _source.SupportedFormats
                .Where(f => f.VideoFormat is not null && f.FrameRate.Denominator > 0)
                .ToList();
            Modes = VideoMode.Options(formats.Select(f => ((int)f.VideoFormat.Width, (int)f.VideoFormat.Height,
                (double)f.FrameRate.Numerator / f.FrameRate.Denominator)));

            if (!Shared && VideoMode.Preferred(Modes, wanted) is VideoMode mode)
            {
                var format = formats
                    .Where(f => Matches(f, mode))
                    .OrderBy(f => SubtypeRank(f.Subtype))
                    .FirstOrDefault();
                if (format is not null) await _source.SetFormatAsync(format);
            }
            var current = _source.CurrentFormat;
            Mode = current?.VideoFormat is { } vf && current.FrameRate.Denominator > 0
                ? new VideoMode((int)vf.Width, (int)vf.Height, (int)Math.Round((double)current.FrameRate.Numerator / current.FrameRate.Denominator))
                : null;

            _reader = await _capture.CreateFrameReaderAsync(_source, MediaEncodingSubtypes.Bgra8);
            _reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _reader.FrameArrived += OnFrameArrived;
            var started = await _reader.StartAsync();
            if (started != MediaFrameReaderStartStatus.Success)
            {
                await StopLockedAsync();
                SetStatus(started == MediaFrameReaderStartStatus.ExclusiveControlNotAvailable
                    ? "The camera is in use by another app." : $"Preview could not start ({started}).");
                return;
            }
            SetStatus("");
        }
        catch (UnauthorizedAccessException)
        {
            await StopLockedAsync();
            SetStatus("Camera access is off. Turn on Settings › Privacy & security › Camera › Let desktop apps access your camera.");
        }
        catch (Exception e)
        {
            await StopLockedAsync();
            SetStatus($"Preview failed: {e.Message}");
        }
    }

    private static async Task<MediaCapture?> OpenAsync(MediaFrameSourceGroup group, MediaCaptureSharingMode mode)
    {
        var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                SharingMode = mode,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video,
            });
            return capture;
        }
        catch (UnauthorizedAccessException)
        {
            capture.Dispose();
            throw;
        }
        catch (Exception)
        {
            capture.Dispose();
            return null;
        }
    }

    private static bool Matches(MediaFrameFormat f, VideoMode m) =>
        f.VideoFormat.Width == m.Width && f.VideoFormat.Height == m.Height &&
        (int)Math.Round((double)f.FrameRate.Numerator / f.FrameRate.Denominator) == m.Fps;

    /// <summary>Uncompressed formats convert to BGRA cheaply; MJPG needs a decode.</summary>
    private static int SubtypeRank(string subtype) => subtype.ToUpperInvariant() switch
    {
        "NV12" => 0,
        "YUY2" => 1,
        "MJPG" => 2,
        _ => 3,
    };

    private async Task StopLockedAsync()
    {
        var reader = _reader;
        _reader = null;
        if (reader is not null)
        {
            reader.FrameArrived -= OnFrameArrived;
            try { await reader.StopAsync(); } catch (Exception) { }
            reader.Dispose();
        }
        _source = null;
        _capture?.Dispose();
        _capture = null;
        Mode = null;
        // Free the last frame (a 4K frame is 33 MB) while the preview is off or the app sits in the tray.
        _bitmap = null;
        target.Source = null;
        StateChanged?.Invoke();
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // Drop frames while the UI is still drawing the previous one.
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return;
        var handed = false;
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            var input = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (input is null) return;
            using var bgra = input.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                ? null : SoftwareBitmap.Convert(input, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            var bmp = bgra ?? input;
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            if (_buffer.Length != w * h * 4) _buffer = new byte[w * h * 4];
            bmp.CopyToBuffer(_buffer.AsBuffer());
            input.Dispose();
            handed = true;
            target.Dispatcher.BeginInvoke(() =>
            {
                try { Draw(w, h); }
                finally { Volatile.Write(ref _pending, 0); }
            });
        }
        catch (Exception) { }
        finally
        {
            if (!handed) Volatile.Write(ref _pending, 0);
        }
    }

    private void Draw(int w, int h)
    {
        if (_reader is null) return;
        if (_bitmap is null || _width != w || _height != h)
        {
            _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            _width = w;
            _height = h;
        }
        // Starting the preview clears the image while "Starting preview…" shows, so attach it again
        // even when the frame size is unchanged (e.g. reopening the window from the tray).
        if (!ReferenceEquals(target.Source, _bitmap)) target.Source = _bitmap;
        _bitmap.WritePixels(new Int32Rect(0, 0, w, h), _buffer, w * 4, 0);
    }

    private void SetStatus(string status)
    {
        Status = status;
        if (status.Length > 0 && _reader is null) target.Source = null;
        StateChanged?.Invoke();
    }
}
