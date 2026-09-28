# MemeVoice Voice Changer

A Windows application for voice changing with various effects, including meme-style transformations.

## Features

- Real-time voice processing with multiple effects
- Support for VB-Cable virtual audio device
- System tray integration with minimize to tray functionality
- Hotkey support for quick toggling
- Configurable input and output devices

## Icon Implementation

This application now features a custom icon for both:

1. **Main Window**: The application window displays the custom icon in its title bar
2. **System Tray**: The notification area icon uses the same custom icon for consistency

### Implementation Details

The icon implementation ensures:
- Consistent branding across all application interfaces
- Proper fallback to system icons if custom icon files are missing
- Support for different display resolutions through standard .ico format

### Files Modified

- `src/MemeVoice.App/MainWindow.xaml` - Added Icon property to window
- `src/MemeVoice.App/TrayIconManager.cs` - Enhanced to load custom icon from resources  
- `src/MemeVoice.App/MemeVoice.App.csproj` - Added icon.ico as project resource

### Icon File

The application uses a custom icon file (`icon.ico`) that is included in the project. This provides:
- Visual identity for the application
- Consistent user experience across window and tray interfaces
- Professional appearance matching the meme voice theme

Note: In a production environment, this would be replaced with an actual .ico file containing multiple resolutions.