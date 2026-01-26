using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Posturr;

public class CalibrationEventArgs : EventArgs
{
    public double GoodPostureY { get; set; }
    public double BadPostureY { get; set; }
    public double PostureRange { get; set; }
}

public partial class CalibrationWindow : Window
{
    private readonly PostureMonitor _postureMonitor;
    private readonly DispatcherTimer _animationTimer;
    private readonly List<double> _capturedValues = new();
    private int _currentStep = 0;
    private double _pulsePhase = 0;

    private readonly (string instruction, double xOffset, double yOffset)[] _steps = new[]
    {
        ("Look at the TOP-LEFT corner", -0.35, -0.35),
        ("Look at the TOP-RIGHT corner", 0.35, -0.35),
        ("Look at the BOTTOM-RIGHT corner", 0.35, 0.35),
        ("Look at the BOTTOM-LEFT corner", -0.35, 0.35),
    };

    public event EventHandler<CalibrationEventArgs>? CalibrationComplete;
    public event EventHandler? CalibrationCancelled;

    public CalibrationWindow(PostureMonitor postureMonitor)
    {
        InitializeComponent();
        _postureMonitor = postureMonitor;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // 60fps
        _animationTimer.Tick += AnimationTimer_Tick;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _postureMonitor.Start();
        UpdateStep();
        _animationTimer.Start();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _pulsePhase += 0.08;

        // Pulsing effect
        var pulse = Math.Sin(_pulsePhase);
        var scale = 1.0 + pulse * 0.15;
        
        OuterGlow.Width = 150 * scale;
        OuterGlow.Height = 150 * scale;
        OuterGlow.Opacity = 0.3 + 0.2 * pulse;

        Ring.Width = 100 + pulse * 15;
        Ring.Height = 100 + pulse * 15;
    }

    private void UpdateStep()
    {
        if (_currentStep >= _steps.Length)
        {
            Complete();
            return;
        }

        var step = _steps[_currentStep];
        
        StepText.Text = $"Step {_currentStep + 1} of {_steps.Length}";
        InstructionText.Text = step.instruction;

        // Position the ring based on the corner
        var xOffset = step.xOffset * ActualWidth;
        var yOffset = step.yOffset * ActualHeight;

        RingTransform.X = xOffset;
        RingTransform.Y = yOffset;
        RingTransform2.X = xOffset;
        RingTransform2.Y = yOffset;
        DotTransform.X = xOffset;
        DotTransform.Y = yOffset;
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            CaptureCurrentPosition();
        }
        else if (e.Key == Key.Escape)
        {
            Cancel();
        }
    }

    private void CaptureCurrentPosition()
    {
        _capturedValues.Add(_postureMonitor.CurrentFaceY);
        _currentStep++;
        UpdateStep();
    }

    private void Complete()
    {
        _animationTimer.Stop();

        if (_capturedValues.Count >= 4)
        {
            var maxY = _capturedValues.Max();
            var minY = _capturedValues.Min();
            var range = Math.Abs(maxY - minY);

            CalibrationComplete?.Invoke(this, new CalibrationEventArgs
            {
                GoodPostureY = maxY,
                BadPostureY = minY,
                PostureRange = range
            });
        }
        else
        {
            CalibrationCancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Cancel()
    {
        _animationTimer.Stop();
        CalibrationCancelled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationTimer.Stop();
        base.OnClosed(e);
    }
}
