# Samsung Mini Remote

A compact **Windows WPF / .NET 8** desktop shell inspired by the supplied Samsung remote screenshot.

## Included

- Compact vertical, dark, rounded remote, placed at the **bottom-right** of the Windows work area at launch.
- Title bar with **minimize** and **close**.
- Close button / Alt+F4 **hide the window to the system tray** (do not exit).
- Double-click tray icon or right-click → **Open remote** to bring it back.
- Right-click tray icon → **Exit** to actually quit.
- Mute, Choose a TV, Power, 5-way D-pad/OK, **Volume Down/Up** rocker, Play/Pause, Back, and Source buttons.
- Icons rendered as **actual Lucide-style vector paths**, not emoji, Unicode characters, or an icon font.
- Buttons are **UI-only placeholders**; no Samsung TV / network functionality implemented yet.

## Run on Windows

Install the **.NET 8 SDK** (Windows Desktop workload comes with the SDK), then open PowerShell in this folder:

```powershell
dotnet run --project .\SamsungMiniRemote.csproj
```

## Build release

```powershell
dotnet publish .\SamsungMiniRemote.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The `.exe` is in `bin\Release\net8.0-windows\win-x64\publish\`.
Keep its `Assets` subfolder beside the executable for the tray icon.

## Notes

- Windows-only WPF project. Requires Windows 10 or 11 to run.
- Change initial window size in `MainWindow.xaml`.
- Positioning is in `MainWindow.xaml.cs`.
- App lifecycle and system tray logic are in `App.xaml.cs`.
- Some anti-virus tools may analyze a newly compiled unsigned desktop executable; no network code exists in this UI-only project.
