# Posturr for Windows

An unofficial, fan-made Windows port of the brilliant [Posturr, now Dorso](https://github.com/tldev/dorso) app for macOS.

## 🙏 Credits & Attribution

**This project is a Windows reimagination of [Posturr / Dorso](https://github.com/tldev/dorso)** - all credit for the original concept, design, and inspiration goes to:

- **[Tom Lancaster](https://github.com/tldev)** - Creator of the original Posturr
- **[Cam](https://github.com/cam-br0wn)** and **Ben** - Credited in this Windows port's original README

The original Posturr is a fantastic macOS app that helps you maintain good posture by detecting when you're slouching and progressively dimming/blurring your screen until you sit up straight. This Windows version attempts to bring that same experience to Windows users.

We are just fans: this port is not affiliated with or endorsed by the upstream project. The upstream creators retain rights to their original work, and [Tom Lancaster and the upstream maintainers](https://github.com/tldev/dorso) decide the direction and other matters of the macOS project. Windows contributors license only their own contributions; this does not transfer ownership of upstream work or change permissions already granted under its license.

**Please check out and star the original project: https://github.com/tldev/dorso** ⭐

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

The source code and documentation are licensed under the [MIT License](LICENSE), matching the [upstream license](https://github.com/tldev/dorso/blob/main/LICENSE). The upstream notice, `Copyright (c) 2025 Posturr Contributors`, is preserved for upstream-derived work; no separate Windows-port copyright notice is added.

MIT permits commercial use, modification, and redistribution of the covered work, subject to preserving the copyright and permission notices and the other terms in `LICENSE`. It does not grant rights to third-party material beyond that material's own license, or permission to imply upstream endorsement.

**Asset and dependency scope:** `posturr.ico` was added in the Windows repository without a recorded source or asset license. Its provenance has not been established, so it is excluded from this new license grant; obtain permission from its rights holder or replace it with an asset of known provenance before redistributing it. The programmatically drawn tray icon is part of the licensed source code. Third-party dependencies, including [Hardcodet.NotifyIcon.Wpf 1.1.0](https://www.nuget.org/packages/Hardcodet.NotifyIcon.Wpf/1.1.0), retain their own licenses and notices. This is not a blanket licensing assurance for every asset, dependency, or historical build artifact.

---

*Built with respect and admiration for the original Posturr team's creativity and execution.* 💪
