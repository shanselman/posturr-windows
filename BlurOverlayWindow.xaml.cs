using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Posturr;

public partial class BlurOverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private IntPtr _hwnd;
    private System.Drawing.Rectangle _savedBounds;
    private double _dpiScale = 1.0;

    public BlurOverlayWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScale = source.CompositionTarget.TransformToDevice.M11;
        }
        if (_dpiScale <= 0) _dpiScale = 1.0;
        
        // Make click-through
        int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        
        this.Visibility = Visibility.Collapsed;
    }

    public void SetBounds(System.Drawing.Rectangle bounds)
    {
        _savedBounds = bounds;
        
        Left = bounds.X / _dpiScale;
        Top = bounds.Y / _dpiScale;
        Width = bounds.Width / _dpiScale;
        Height = bounds.Height / _dpiScale;
    }

    public void SetBlurOpacity(double opacity)
    {
        if (opacity <= 0.01)
        {
            this.Visibility = Visibility.Collapsed;
            DimRect.Opacity = 0;
        }
        else
        {
            Left = _savedBounds.X / _dpiScale;
            Top = _savedBounds.Y / _dpiScale;
            
            this.Visibility = Visibility.Visible;
            
            // Simple dim - max 70% opacity so screen is still visible
            DimRect.Opacity = Math.Min(0.7, opacity * 0.7);
        }
    }
}
