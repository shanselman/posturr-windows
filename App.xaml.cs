using System.Windows;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfApplication = System.Windows.Application;

namespace Posturr;

public partial class App : WpfApplication
{
    private TaskbarIcon? _notifyIcon;
    private PostureMonitor? _postureMonitor;
    private List<BlurOverlayWindow> _blurWindows = new();
    private DispatcherTimer? _blurTimer;
    private CalibrationWindow? _calibrationWindow;

    private int _currentBlurRadius = 0;
    private int _targetBlurRadius = 0;
    private bool _isEnabled = true;
    private bool _isCalibrated = false;

    // Settings
    private double _sensitivity = 0.85;
    private double _deadZone = 0.03;
    private bool _blurWhenAway = false;
    private string? _selectedCameraId;

    // Settings file path
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Posturr", "settings.json");

    // Menu items for state updates
    private WpfMenuItem? _statusMenuItem;
    private WpfMenuItem? _enabledMenuItem;
    private WpfMenuItem? _blurWhenAwayMenuItem;
    private WpfMenuItem? _cameraMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        try
        {
            Debug.WriteLine("Posturr starting...");
            LoadSettings();
            Debug.WriteLine("Settings loaded");
            SetupNotifyIcon();
            Debug.WriteLine("NotifyIcon setup complete");
            SetupBlurOverlays();
            Debug.WriteLine("Blur overlays setup complete");
            SetupBlurTimer();
            Debug.WriteLine("Blur timer setup complete");
            
            _postureMonitor = new PostureMonitor();
            _postureMonitor.PostureChanged += OnPostureChanged;
            _postureMonitor.StatusChanged += OnStatusChanged;
            _postureMonitor.Sensitivity = _sensitivity;
            _postureMonitor.DeadZone = _deadZone;
            _postureMonitor.SelectedCameraId = _selectedCameraId;
            Debug.WriteLine("PostureMonitor setup complete");
            
            // Start calibration after a short delay
            var startTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            startTimer.Tick += (s, args) =>
            {
                startTimer.Stop();
                StartCalibration();
            };
            startTimer.Start();
            Debug.WriteLine("Startup complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Startup error: {ex}");
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}", "Posturr Error");
        }
    }

    private void SetupNotifyIcon()
    {
        var contextMenu = new System.Windows.Controls.ContextMenu();

        _statusMenuItem = new WpfMenuItem { Header = "Status: Starting...", IsEnabled = false };
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        _enabledMenuItem = new WpfMenuItem { Header = "Enabled", IsCheckable = true, IsChecked = true };
        _enabledMenuItem.Click += (s, e) => ToggleEnabled();
        contextMenu.Items.Add(_enabledMenuItem);

        var recalibrateItem = new WpfMenuItem { Header = "Recalibrate" };
        recalibrateItem.Click += (s, e) => StartCalibration();
        contextMenu.Items.Add(recalibrateItem);

        // Camera submenu
        _cameraMenuItem = new WpfMenuItem { Header = "Camera" };
        _cameraMenuItem.SubmenuOpened += (s, e) => RefreshCameraMenu();
        contextMenu.Items.Add(_cameraMenuItem);
        
        // Add placeholder so submenu arrow shows
        _cameraMenuItem.Items.Add(new WpfMenuItem { Header = "Loading...", IsEnabled = false });

        // Sensitivity submenu
        var sensitivityMenu = new WpfMenuItem { Header = "Sensitivity" };
        var sensitivityOptions = new[] {
            ("Very Low — Only major slouching", 0.4),
            ("Low — Allows more movement", 0.6),
            ("Medium — Balanced", 0.85),
            ("High — Reacts to small changes", 0.95),
            ("Very High — Maximum response", 1.0)
        };
        foreach (var (label, value) in sensitivityOptions)
        {
            var item = new WpfMenuItem { Header = label, IsCheckable = true, IsChecked = Math.Abs(_sensitivity - value) < 0.01 };
            var capturedValue = value;
            item.Click += (s, e) =>
            {
                _sensitivity = capturedValue;
                if (_postureMonitor != null) _postureMonitor.Sensitivity = _sensitivity;
                foreach (WpfMenuItem mi in sensitivityMenu.Items)
                    mi.IsChecked = mi == s;
            };
            sensitivityMenu.Items.Add(item);
        }
        contextMenu.Items.Add(sensitivityMenu);

        // Dead Zone submenu
        var deadZoneMenu = new WpfMenuItem { Header = "Dead Zone" };
        var deadZoneOptions = new[] {
            ("Very Small — Activates immediately", 0.01),
            ("Small — Strict enforcement", 0.02),
            ("Medium — Balanced", 0.03),
            ("Large — Allows natural movement", 0.05),
            ("Very Large — Only major slouching", 0.08)
        };
        foreach (var (label, value) in deadZoneOptions)
        {
            var item = new WpfMenuItem { Header = label, IsCheckable = true, IsChecked = Math.Abs(_deadZone - value) < 0.001 };
            var capturedValue = value;
            item.Click += (s, e) =>
            {
                _deadZone = capturedValue;
                if (_postureMonitor != null) _postureMonitor.DeadZone = _deadZone;
                foreach (WpfMenuItem mi in deadZoneMenu.Items)
                    mi.IsChecked = mi == s;
            };
            deadZoneMenu.Items.Add(item);
        }
        contextMenu.Items.Add(deadZoneMenu);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        _blurWhenAwayMenuItem = new WpfMenuItem { Header = "Blur When Away", IsCheckable = true, IsChecked = _blurWhenAway };
        _blurWhenAwayMenuItem.Click += (s, e) =>
        {
            _blurWhenAway = _blurWhenAwayMenuItem.IsChecked;
            if (_postureMonitor != null) _postureMonitor.BlurWhenAway = _blurWhenAway;
        };
        contextMenu.Items.Add(_blurWhenAwayMenuItem);

        // Test blur button
        var testBlurItem = new WpfMenuItem { Header = "Test Blur (5 sec)" };
        testBlurItem.Click += (s, e) => TestBlur();
        contextMenu.Items.Add(testBlurItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var quitItem = new WpfMenuItem { Header = "Quit" };
        quitItem.Click += (s, e) => Quit();
        contextMenu.Items.Add(quitItem);

        _notifyIcon = new TaskbarIcon
        {
            Icon = CreateIcon(),
            ToolTipText = "Posturr - Posture Monitor",
            ContextMenu = contextMenu
        };
        
        // Fix context menu placement for multi-monitor
        contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
    }

    private Icon CreateIcon()
    {
        // Create a simple icon programmatically
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        
        // Draw a simple person figure
        using var pen = new Pen(Color.White, 2);
        // Head
        g.DrawEllipse(pen, 12, 2, 8, 8);
        // Body
        g.DrawLine(pen, 16, 10, 16, 20);
        // Arms
        g.DrawLine(pen, 16, 13, 8, 17);
        g.DrawLine(pen, 16, 13, 24, 17);
        // Legs
        g.DrawLine(pen, 16, 20, 10, 30);
        g.DrawLine(pen, 16, 20, 22, 30);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void SetupBlurOverlays()
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var blurWindow = new BlurOverlayWindow();
            blurWindow.SetBounds(screen.Bounds);
            blurWindow.Show();
            _blurWindows.Add(blurWindow);
        }
    }

    private void SetupBlurTimer()
    {
        _blurTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) }; // ~30fps
        _blurTimer.Tick += (s, e) => UpdateBlur();
        _blurTimer.Start();
    }

    private void UpdateBlur()
    {
        // Smooth transition
        if (_currentBlurRadius < _targetBlurRadius)
        {
            _currentBlurRadius = Math.Min(_currentBlurRadius + 1, _targetBlurRadius);
        }
        else if (_currentBlurRadius > _targetBlurRadius)
        {
            _currentBlurRadius = Math.Max(_currentBlurRadius - 3, _targetBlurRadius);
        }

        var opacity = Math.Min(1.0, Math.Sqrt(_currentBlurRadius / 64.0) * 1.2);
        Debug.WriteLine($"Blur: target={_targetBlurRadius}, current={_currentBlurRadius}, opacity={opacity:F2}");
        foreach (var window in _blurWindows)
        {
            window.SetBlurOpacity(opacity);
        }
    }

    private void OnPostureChanged(object? sender, PostureEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_isEnabled || !_isCalibrated) return;

            _targetBlurRadius = e.BlurLevel;
        });
    }

    private void OnStatusChanged(object? sender, StatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_statusMenuItem != null)
                _statusMenuItem.Header = $"Status: {e.Status}";
        });
    }

    private async void RefreshCameraMenu()
    {
        if (_cameraMenuItem == null) return;
        
        _cameraMenuItem.Items.Clear();
        
        try
        {
            if (_postureMonitor == null)
            {
                var waitingItem = new WpfMenuItem { Header = "Loading...", IsEnabled = false };
                _cameraMenuItem.Items.Add(waitingItem);
                return;
            }
            
            var cameras = await _postureMonitor.GetAvailableCamerasAsync();
            
            // Clear again in case menu was rebuilt while we were async
            _cameraMenuItem.Items.Clear();
            
            if (cameras == null || cameras.Count == 0)
            {
                var noCamera = new WpfMenuItem { Header = "No cameras found", IsEnabled = false };
                _cameraMenuItem.Items.Add(noCamera);
                return;
            }

            var currentCameraId = _postureMonitor.SelectedCameraId;
            
            for (int i = 0; i < cameras.Count; i++)
            {
                var camera = cameras[i];
                var item = new WpfMenuItem 
                { 
                    Header = camera.Name, 
                    IsCheckable = true,
                    IsChecked = camera.Id == currentCameraId || (currentCameraId == null && i == 0)
                };
                var capturedId = camera.Id;
                item.Click += (s, e) => SelectCamera(capturedId, item);
                _cameraMenuItem.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error refreshing camera menu: {ex.Message}");
            _cameraMenuItem.Items.Clear();
            var errorItem = new WpfMenuItem { Header = $"Error: {ex.Message}", IsEnabled = false };
            _cameraMenuItem.Items.Add(errorItem);
        }
    }

    private void SelectCamera(string cameraId, WpfMenuItem selectedItem)
    {
        if (_postureMonitor == null || _cameraMenuItem == null) return;
        
        // Update checkmarks
        foreach (var item in _cameraMenuItem.Items.OfType<WpfMenuItem>())
        {
            item.IsChecked = item == selectedItem;
        }
        
        // Switch camera
        _postureMonitor.SelectedCameraId = cameraId;
        _selectedCameraId = cameraId;
        SaveSettings();
        
        _postureMonitor.Stop();
        _postureMonitor.Start();
        
        // Trigger recalibration with new camera
        StartCalibration();
    }

    private void ToggleEnabled()
    {
        _isEnabled = !_isEnabled;
        if (_enabledMenuItem != null)
            _enabledMenuItem.IsChecked = _isEnabled;

        if (!_isEnabled)
        {
            _targetBlurRadius = 0;
            _postureMonitor?.Stop();
            if (_statusMenuItem != null)
                _statusMenuItem.Header = "Status: Disabled";
        }
        else
        {
            _postureMonitor?.Start();
            if (_statusMenuItem != null)
                _statusMenuItem.Header = "Status: Monitoring...";
        }
    }

    private void StartCalibration()
    {
        _isEnabled = false;
        _targetBlurRadius = 0;
        if (_statusMenuItem != null)
            _statusMenuItem.Header = "Status: Calibrating...";

        _calibrationWindow = new CalibrationWindow(_postureMonitor!);
        _calibrationWindow.CalibrationComplete += OnCalibrationComplete;
        _calibrationWindow.CalibrationCancelled += OnCalibrationCancelled;
        _calibrationWindow.Show();
    }

    private void OnCalibrationComplete(object? sender, CalibrationEventArgs e)
    {
        _calibrationWindow?.Close();
        _calibrationWindow = null;

        if (_postureMonitor != null)
        {
            _postureMonitor.SetCalibration(e.GoodPostureY, e.BadPostureY, e.PostureRange);
        }

        _isCalibrated = true;
        _isEnabled = true;
        if (_enabledMenuItem != null)
            _enabledMenuItem.IsChecked = true;
        if (_statusMenuItem != null)
            _statusMenuItem.Header = "Status: Calibrated";

        _postureMonitor?.Start();
    }

    private void OnCalibrationCancelled(object? sender, EventArgs e)
    {
        _calibrationWindow?.Close();
        _calibrationWindow = null;

        _isCalibrated = true; // Use defaults
        _isEnabled = true;
        if (_enabledMenuItem != null)
            _enabledMenuItem.IsChecked = true;
        if (_statusMenuItem != null)
            _statusMenuItem.Header = "Status: Using defaults";

        _postureMonitor?.Start();
    }

    private void TestBlur()
    {
        // Test blur for 5 seconds
        Debug.WriteLine("TestBlur called - setting targetBlurRadius to 64");
        _targetBlurRadius = 64;
        if (_statusMenuItem != null)
            _statusMenuItem.Header = "Status: Test Blur...";
        
        var testTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        testTimer.Tick += (s, e) =>
        {
            testTimer.Stop();
            _targetBlurRadius = 0;
            Debug.WriteLine("Test blur complete - setting targetBlurRadius to 0");
            if (_statusMenuItem != null)
                _statusMenuItem.Header = "Status: Test Complete";
        };
        testTimer.Start();
    }

    private void Quit()
    {
        _postureMonitor?.Stop();
        _postureMonitor?.Dispose();
        _blurTimer?.Stop();
        
        foreach (var window in _blurWindows)
            window.Close();
        
        _notifyIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _selectedCameraId = settings.SelectedCameraId;
                    _sensitivity = settings.Sensitivity;
                    _deadZone = settings.DeadZone;
                    _blurWhenAway = settings.BlurWhenAway;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var settings = new AppSettings
            {
                SelectedCameraId = _selectedCameraId,
                Sensitivity = _sensitivity,
                DeadZone = _deadZone,
                BlurWhenAway = _blurWhenAway
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}

public class AppSettings
{
    public string? SelectedCameraId { get; set; }
    public double Sensitivity { get; set; } = 0.85;
    public double DeadZone { get; set; } = 0.03;
    public bool BlurWhenAway { get; set; } = false;
}
