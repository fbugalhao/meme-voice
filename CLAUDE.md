# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Windows voice changer application called "MemeVoice" that allows real-time audio processing with various meme-style effects. The application uses VB-Cable virtual audio device for routing audio and supports system tray integration, hotkey shortcuts, and configuration persistence.

## Architecture and Structure

The project follows a clear separation of concerns:

1. **MemeVoice.App** - The WPF desktop application that provides the UI and user interaction
2. **MemeVoice.Core** - The core audio processing logic with effects and audio engine

Key components:
- MainWindow.xaml: Main UI with device selection, effect list, and controls
- TrayIconManager.cs: System tray integration for minimize to tray functionality  
- AudioEngine.cs: Core audio processing using NAudio library
- EffectChain.cs: Chain of audio effects that can be applied
- Various voice effects in Effects/ directory (RobotEffect, AlienEffect, PitchShiftEffect, etc.)
- DeviceManager.cs: Handles audio device enumeration and VB-Cable detection

## Build and Development Instructions

### Prerequisites
- .NET 8.0 SDK for Windows
- Visual Studio or JetBrains Rider with WPF support
- VB-Audio Virtual Cable installed for full functionality

### Building
```bash
dotnet build
```

### Running
```bash
dotnet run --project src/MemeVoice.App
```

### Testing
The project uses NAudio and SoundTouch libraries for audio processing. Unit tests can be added to test individual effects and the effect chain, but no existing tests are visible in the codebase.

## Key Files and Implementation Details

- **MainWindow.xaml.cs**: Main application logic with device selection, effect switching, and hotkey handling
- **TrayIconManager.cs**: Implements system tray integration with custom icon loading from embedded resources
- **AudioEngine.cs**: Core audio processing pipeline using WASAPI for low-latency real-time processing
- **EffectChain.cs**: Chain of responsibility pattern for applying multiple effects to audio data

The application features:
- Real-time voice processing with multiple effects
- Support for VB-Cable virtual audio device
- System tray integration with minimize to tray functionality  
- Hotkey support for quick toggling and effect switching
- Configurable input and output devices
- Custom icon implementation that works in both main window and system tray

## Icon Implementation Details

The application implements a custom icon using:
1. `icon.ico` file included as a project resource 
2. Embedded resource loading in TrayIconManager.cs with fallback to system icons
3. Icon property set on MainWindow.xaml
4. Proper fallback handling for when the custom icon file is missing

## Audio Processing Pipeline

The audio processing pipeline:
1. Uses WasapiCapture for real-time audio capture from input device
2. Processes audio through EffectChain with selected effects  
3. Routes processed audio to WasapiOut for output through VB-Cable
4. Handles sample rate conversion and buffer management
5. Includes error handling and logging for audio issues

The pipeline supports various audio formats and includes proper disposal of audio resources.