# S-Remote

A compact Windows desktop remote control for Samsung Smart TVs, built with WPF and .NET 8.

## Features

- **Samsung TV Control**: Connects via WebSocket to control modern Samsung Smart TVs (power, volume, navigation, playback, and home).
- **Auto-Discovery**: Discovers compatible Samsung TVs on the local network via SSDP / UPnP.
- **Wake-on-LAN**: Supports powering on compatible TVs over the local network via WoL magic packets.
- **System Tray Integration**: Minimize or close to the system tray to keep the remote quickly accessible without cluttering the taskbar.
- **Clean Vector UI**: Dark theme styled with custom Lucide-based vector icon paths.

## Requirements

- Windows 10 or 11 (x64)
- .NET 8 Desktop Runtime or .NET 8 SDK

## Running from Source

Clone the repository and run using the .NET CLI:

```powershell
dotnet run --project .\SamsungMiniRemote.csproj
```

Or execute `Run.cmd`.

## Building a Release

To publish a standalone release package:

```powershell
dotnet publish SamsungMiniRemote.csproj -c Release -r win-x64 --self-contained false -o publish
```

Or run `Build-Release.cmd`. The compiled executable will be placed in the `publish\` directory.

## Installer

The project includes an Inno Setup script (`build.iss`) to generate a Windows installer:

1. Build the release output into `publish\`.
2. Open `build.iss` in Inno Setup and compile.

## License

This project is licensed under the [MIT License](LICENSE). Third-party license notices are documented in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
