# Posturr for Windows

A Windows port of the brilliant [Posturr](https://github.com/tldev/posturr) app for macOS.

## 🙏 Credits & Attribution

**This project is a Windows reimagination of [Posturr](https://github.com/tldev/posturr)** - all credit for the original concept, design, and inspiration goes to:

- **[Tom Lancaster](https://github.com/tldev)** - Creator of the original Posturr
- **Cam** and **Ben** - Contributors to the original project

The original Posturr is a fantastic macOS app that helps you maintain good posture by detecting when you're slouching and progressively dimming/blurring your screen until you sit up straight. This Windows version attempts to bring that same experience to Windows users.

**Please check out and star the original project: https://github.com/tldev/posturr** ⭐

## How It Works

1. **Calibrate** - Look at each corner of your screen while sitting with good posture
2. **Monitor** - The app uses your webcam to track your face position
3. **Correct** - When you slouch, the screen dims to remind you to sit up straight

## Features

- 🎥 Webcam-based face detection using Windows FaceDetector API
- 📐 4-corner calibration for accurate posture detection
- 🖥️ Multi-monitor support
- ⚙️ Adjustable sensitivity and dead zone settings
- 💾 Persistent settings (camera selection, preferences)
- 🔔 System tray integration

## Requirements

- Windows 10 (build 19041) or later
- .NET 10.0 Runtime
- A webcam

## Building

```powershell
dotnet build
dotnet run
```

## Usage

1. Run the app - it will appear in your system tray
2. Right-click the tray icon and select "Calibrate"
3. Follow the on-screen instructions to look at each corner
4. The app will now monitor your posture and dim the screen when you slouch

## Settings

- **Sensitivity** - How quickly the screen dims (Low/Medium/High)
- **Dead Zone** - How much movement is allowed before triggering (Small/Medium/Large)
- **Blur When Away** - Whether to dim when no face is detected
- **Camera** - Select which webcam to use

## Technical Notes

The Windows version uses:
- **Windows.Media.FaceAnalysis.FaceDetector** for face detection
- **MediaFrameReader** for efficient webcam capture
- **WPF** for the UI and overlay windows

Note: Unlike the macOS version which uses private CoreGraphics APIs for blur effects, the Windows version uses a dim overlay. True blur effects on Windows require DirectX composition which adds significant complexity.

## License

This project follows the same spirit as the original Posturr. Please see the [original repository](https://github.com/tldev/posturr) for licensing information.

---

*Built with respect and admiration for the original Posturr team's creativity and execution.* 💪
