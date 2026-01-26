using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.FaceAnalysis;
using Windows.Graphics.Imaging;
using Windows.Devices.Enumeration;

namespace Posturr;

public class CameraInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class PostureEventArgs : EventArgs
{
    public int BlurLevel { get; set; }
    public bool IsSlouching { get; set; }
}

public class StatusEventArgs : EventArgs
{
    public string Status { get; set; } = "";
}

public class PostureMonitor : IDisposable
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private FaceDetector? _faceDetector;
    private bool _isRunning;
    private bool _disposed;

    // Camera selection
    public string? SelectedCameraId { get; set; }

    // Calibration values
    private double _goodPostureY = 0.6;
    private double _badPostureY = 0.4;
    private double _neutralY = 0.5;
    private double _postureRange = 0.2;

    // Settings
    public double Sensitivity { get; set; } = 0.85;
    public double DeadZone { get; set; } = 0.03;
    public bool BlurWhenAway { get; set; } = false;

    // Detection state
    private int _consecutiveBadFrames = 0;
    private int _consecutiveGoodFrames = 0;
    private int _consecutiveNoDetectionFrames = 0;
    private const int FrameThreshold = 8;
    private const int AwayFrameThreshold = 15;
    private bool _isCurrentlySlouching = false;

    // Smoothing
    private List<double> _faceYHistory = new();
    private const int SmoothingWindow = 5;
    private double _currentFaceY = 0.5;

    // Frame throttling
    private DateTime _lastFrameTime = DateTime.MinValue;
    private readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(100); // 10fps

    public event EventHandler<PostureEventArgs>? PostureChanged;
    public event EventHandler<StatusEventArgs>? StatusChanged;
    public event EventHandler<double>? FaceYChanged;

    public PostureMonitor()
    {
    }

    public async Task<List<CameraInfo>> GetAvailableCamerasAsync()
    {
        var cameras = new List<CameraInfo>();
        
        try
        {
            // Use MediaFrameSourceGroup which is more reliable for cameras
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
            foreach (var group in frameSourceGroups)
            {
                // Only include groups that have video sources
                bool hasVideoSource = false;
                foreach (var sourceInfo in group.SourceInfos)
                {
                    if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview ||
                        sourceInfo.MediaStreamType == MediaStreamType.VideoRecord)
                    {
                        hasVideoSource = true;
                        break;
                    }
                }
                
                if (hasVideoSource)
                {
                    cameras.Add(new CameraInfo { Id = group.Id, Name = group.DisplayName });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting cameras: {ex.Message}");
        }
        
        return cameras;
    }

    public async void Start()
    {
        if (_isRunning) return;

        try
        {
            // Initialize face detector
            if (FaceDetector.IsSupported)
            {
                _faceDetector = await FaceDetector.CreateAsync();
            }
            else
            {
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Face detection not supported" });
                return;
            }

            // Initialize media capture
            _mediaCapture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            await _mediaCapture.InitializeAsync(settings);

            // Find color frame source - prefer selected camera if specified
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
            MediaFrameSourceGroup? selectedGroup = null;
            MediaFrameSourceInfo? selectedSourceInfo = null;

            foreach (var group in frameSourceGroups)
            {
                // If a specific camera is selected, look for it by exact ID match
                if (!string.IsNullOrEmpty(SelectedCameraId))
                {
                    if (group.Id == SelectedCameraId)
                    {
                        foreach (var sourceInfo in group.SourceInfos)
                        {
                            if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview ||
                                sourceInfo.MediaStreamType == MediaStreamType.VideoRecord)
                            {
                                selectedGroup = group;
                                selectedSourceInfo = sourceInfo;
                                break;
                            }
                        }
                        if (selectedGroup != null) break;
                    }
                }
                else
                {
                    // No specific camera selected, use first available
                    foreach (var sourceInfo in group.SourceInfos)
                    {
                        if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview ||
                            sourceInfo.MediaStreamType == MediaStreamType.VideoRecord)
                        {
                            selectedGroup = group;
                            selectedSourceInfo = sourceInfo;
                            break;
                        }
                    }
                    if (selectedGroup != null) break;
                }
            }

            // If selected camera not found, fall back to first available
            if (selectedGroup == null && !string.IsNullOrEmpty(SelectedCameraId))
            {
                foreach (var group in frameSourceGroups)
                {
                    foreach (var sourceInfo in group.SourceInfos)
                    {
                        if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview ||
                            sourceInfo.MediaStreamType == MediaStreamType.VideoRecord)
                        {
                            selectedGroup = group;
                            selectedSourceInfo = sourceInfo;
                            break;
                        }
                    }
                    if (selectedGroup != null) break;
                }
            }

            if (selectedGroup == null || selectedSourceInfo == null)
            {
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = "No Camera" });
                return;
            }

            // Re-initialize with the specific source
            _mediaCapture = new MediaCapture();
            var initSettings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };
            await _mediaCapture.InitializeAsync(initSettings);
            
            // Store the actual camera being used
            if (string.IsNullOrEmpty(SelectedCameraId))
            {
                SelectedCameraId = selectedGroup.Id;
            }

            var frameSource = _mediaCapture.FrameSources[selectedSourceInfo.Id];
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource);
            _frameReader.FrameArrived += FrameReader_FrameArrived;

            var status = await _frameReader.StartAsync();
            if (status == MediaFrameReaderStartStatus.Success)
            {
                _isRunning = true;
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Monitoring..." });
            }
            else
            {
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = $"Camera error: {status}" });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Camera initialization error: {ex.Message}");
            StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Camera Error" });
        }
    }

    public async void Stop()
    {
        _isRunning = false;
        
        if (_frameReader != null)
        {
            _frameReader.FrameArrived -= FrameReader_FrameArrived;
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _frameReader = null;
        }

        _mediaCapture?.Dispose();
        _mediaCapture = null;
    }

    private async void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (!_isRunning || _faceDetector == null) return;

        var now = DateTime.Now;
        if (now - _lastFrameTime < _frameInterval) return;
        _lastFrameTime = now;

        try
        {
            using var frameRef = sender.TryAcquireLatestFrame();
            if (frameRef?.VideoMediaFrame?.SoftwareBitmap == null) return;

            var bitmap = frameRef.VideoMediaFrame.SoftwareBitmap;
            
            // Convert to Gray8 if needed (FaceDetector requires Gray8 or Nv12)
            SoftwareBitmap? convertedBitmap = null;
            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Gray8 &&
                bitmap.BitmapPixelFormat != BitmapPixelFormat.Nv12)
            {
                if (FaceDetector.IsBitmapPixelFormatSupported(BitmapPixelFormat.Gray8))
                {
                    convertedBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Gray8);
                }
                else
                {
                    return; // Can't process this format
                }
            }

            var bitmapToProcess = convertedBitmap ?? bitmap;
            var faces = await _faceDetector.DetectFacesAsync(bitmapToProcess);
            
            convertedBitmap?.Dispose();

            if (faces.Count > 0)
            {
                var face = faces[0];
                var frameHeight = bitmapToProcess.PixelHeight;
                
                // Calculate normalized face Y position (0 = bottom, 1 = top)
                var faceCenterY = face.FaceBox.Y + face.FaceBox.Height / 2.0;
                var normalizedY = 1.0 - (faceCenterY / frameHeight);

                _currentFaceY = normalizedY;
                FaceYChanged?.Invoke(this, normalizedY);

                _consecutiveNoDetectionFrames = 0;
                EvaluatePosture(normalizedY);
            }
            else
            {
                HandleNoDetection();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Frame processing error: {ex.Message}");
        }
    }

    private void HandleNoDetection()
    {
        _consecutiveNoDetectionFrames++;
        _consecutiveBadFrames = 0;
        _consecutiveGoodFrames = 0;

        if (!BlurWhenAway)
        {
            _consecutiveNoDetectionFrames = 0;
            return;
        }

        if (_consecutiveNoDetectionFrames >= AwayFrameThreshold)
        {
            PostureChanged?.Invoke(this, new PostureEventArgs { BlurLevel = 64, IsSlouching = true });
            StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Away" });
        }
    }

    private double SmoothFaceY(double rawY)
    {
        _faceYHistory.Add(rawY);
        if (_faceYHistory.Count > SmoothingWindow)
            _faceYHistory.RemoveAt(0);

        return _faceYHistory.Average();
    }

    private void EvaluatePosture(double currentY)
    {
        var smoothedY = SmoothFaceY(currentY);

        var slouchAmount = _badPostureY - smoothedY;

        var baseThreshold = DeadZone * _postureRange * Sensitivity;
        var enterThreshold = baseThreshold;
        var exitThreshold = baseThreshold * 0.5;

        var threshold = _isCurrentlySlouching ? exitThreshold : enterThreshold;
        var isBadPosture = slouchAmount > threshold;

        if (isBadPosture)
        {
            _consecutiveBadFrames++;
            _consecutiveGoodFrames = 0;

            if (_consecutiveBadFrames >= FrameThreshold)
            {
                _isCurrentlySlouching = true;

                var severity = (slouchAmount - enterThreshold) / _postureRange;
                var clampedSeverity = Math.Clamp(severity, 0.0, 1.0);
                var easedSeverity = clampedSeverity * clampedSeverity;

                var blurIntensity = (int)(2 + easedSeverity * 62 * Sensitivity);
                blurIntensity = Math.Min(64, blurIntensity);

                PostureChanged?.Invoke(this, new PostureEventArgs { BlurLevel = blurIntensity, IsSlouching = true });
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Slouching" });
            }
        }
        else
        {
            _consecutiveGoodFrames++;
            _consecutiveBadFrames = 0;

            PostureChanged?.Invoke(this, new PostureEventArgs { BlurLevel = 0, IsSlouching = false });

            if (_consecutiveGoodFrames >= FrameThreshold)
            {
                _isCurrentlySlouching = false;
                StatusChanged?.Invoke(this, new StatusEventArgs { Status = "Good Posture" });
            }
        }
    }

    public void SetCalibration(double goodY, double badY, double range)
    {
        _goodPostureY = goodY;
        _badPostureY = badY;
        _neutralY = (goodY + badY) / 2;
        _postureRange = range;

        _consecutiveBadFrames = 0;
        _consecutiveGoodFrames = 0;
        _isCurrentlySlouching = false;
    }

    public double CurrentFaceY => _currentFaceY;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
