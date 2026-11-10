# Icon Integration Guide

## Overview
TaggJack now uses `icon.png` as both the **application icon** and **system tray icon**, providing consistent branding across all visual elements.

## Icon Files

### icon.png
- **Purpose**: Source image for icons
- **Location**: Root directory (`E:\Repos\TaggJack\icon.png`)
- **Usage**:
  - System tray icon (displayed in taskbar notification area)
  - Source for ICO conversion
  - Resource embedded in application

### icon.ico
- **Purpose**: Windows application icon
- **Location**: Root directory (`E:\Repos\TaggJack\icon.ico`)
- **Sizes included**: 16×16, 32×32, 48×48, 128×128 pixels
- **Usage**:
  - Application window icon
  - Taskbar icon
  - Alt+Tab icon
  - Start menu/desktop shortcut icon
  - File explorer icon for EXE

## Implementation Details

### 1. Application Icon (icon.ico)

**Updated in `TaggJack.csproj`:**
```xml
<PropertyGroup>
  <ApplicationIcon>icon.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <Resource Include="icon.png" />
  <Content Include="icon.png">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

**What this does:**
- Sets `icon.ico` as the executable's icon
- Embeds `icon.png` as a resource in the assembly
- Copies `icon.png` to output directory for runtime access

### 2. System Tray Icon (icon.png)

**Updated in `Services/SystemTrayService.cs`:**
```csharp
// Try to load icon from file path
var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");

if (File.Exists(iconPath))
{
    _notifyIcon.IconSource = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
}
else
{
    // Fallback to pack URI
    _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/icon.png"));
}
```

**What this does:**
- First attempts to load `icon.png` from application directory
- Falls back to embedded resource if file not found
- Uses PNG format for better quality in system tray

### 3. About Window Icon (icon.png)

**Updated in `UI/AboutWindow.xaml`:**
```xaml
<!-- Logo/Icon -->
<Border Grid.Row="0" Width="100" Height="100" 
        HorizontalAlignment="Center" Margin="0,0,0,16">
    <Image Source="/icon.png" 
           Stretch="Uniform"
           RenderOptions.BitmapScalingMode="HighQuality"/>
</Border>

<!-- Application Name -->
<TextBlock Grid.Row="1" Text="TaggJack" FontSize="28" FontWeight="Bold" 
           Foreground="#0078d4" HorizontalAlignment="Center" Margin="0,0,0,4"/>
```

**What this does:**
- Displays icon.png prominently in the About dialog
- 100×100 pixel display size for good visibility
- High-quality rendering for crisp appearance
- Consistent branding with rest of application

## Icon Conversion Script

### convert-icon.ps1

A PowerShell script is provided to convert `icon.png` to `icon.ico`:

```powershell
powershell -ExecutionPolicy Bypass -File convert-icon.ps1
```

**Features:**
- Converts PNG to multi-resolution ICO
- Creates 4 sizes: 16, 32, 48, 128 pixels
- High-quality bicubic interpolation
- Automatic output to `icon.ico`

**Usage:**
```powershell
# Basic usage
.\convert-icon.ps1

# Custom input/output
.\convert-icon.ps1 -InputFile "myicon.png" -OutputFile "myapp.ico"
```

## Icon Requirements

### For Best Results

**Recommended icon.png specifications:**
- **Size**: 256×256 pixels or larger
- **Format**: PNG with transparency
- **Color depth**: 32-bit RGBA
- **Design**: Simple, clear, recognizable at small sizes

**Design tips:**
- Keep design simple (avoid fine details)
- Use high contrast colors
- Ensure visibility at 16×16 pixels
- Test on both light and dark backgrounds
- Avoid text (usually unreadable at small sizes)

## Where Icons Appear

### Application Icon (icon.ico)
1. **Window title bar** - Top-left corner of main window
2. **Taskbar** - When application is running
3. **Alt+Tab switcher** - Application thumbnail
4. **Start menu** - Pinned or in app list
5. **Desktop shortcut** - If created
6. **File explorer** - TaggJack.exe icon
7. **Task Manager** - Process list

### System Tray Icon (icon.png)
1. **System tray** - Notification area (bottom-right)
2. **Tray menu** - Context menu icon
3. **Balloon notifications** - Toast notifications
4. **About window** - Help → About dialog

## Updating Icons

### To change the icon:

1. **Replace icon.png** with your new image
   ```bash
   # Backup old icon
   copy icon.png icon.backup.png
   
   # Copy new icon
   copy my-new-icon.png icon.png
   ```

2. **Regenerate icon.ico**
   ```powershell
   powershell -ExecutionPolicy Bypass -File convert-icon.ps1
   ```

3. **Rebuild the application**
   ```bash
   dotnet build
   ```

4. **Test both icons**
   - Run application and check window icon
   - Verify system tray icon appears correctly

## Troubleshooting

### Application icon not showing
- Verify `icon.ico` exists in project root
- Check `TaggJack.csproj` has `<ApplicationIcon>icon.ico</ApplicationIcon>`
- Rebuild solution: `dotnet clean` then `dotnet build`
- Clear Windows icon cache (see below)

### System tray icon not showing
- Verify `icon.png` exists in project root
- Check output directory has `icon.png` copied
- Review `tray-debug.log` for icon loading errors
- Ensure PNG has transparency support

### Icons cached by Windows
Windows caches icons. To clear:

```powershell
# Clear icon cache
taskkill /f /im explorer.exe
cd %userprofile%\AppData\Local
del IconCache.db /a
start explorer.exe
```

Or restart Windows.

### Wrong icon showing
- Windows may cache old icon
- Delete `bin` and `obj` folders
- Rebuild: `dotnet clean && dotnet build`
- Clear Windows icon cache (above)

## Build Integration

### Debug Builds
Icons are automatically copied to: `bin\Debug\net9.0-windows\win-x64\`

### Release Builds
Icons are automatically copied to: `bin\Release\net9.0-windows\win-x64\`

### Published Builds
When publishing (`dotnet publish`), icons are included:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

Published executable in `publish\` folder will have icon embedded.

## Installer Integration

### Inno Setup Integration

Update `TaggJack-Setup.iss` to use icon:

```iss
[Setup]
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\TaggJack.exe

[Icons]
Name: "{autoprograms}\TaggJack"; Filename: "{app}\TaggJack.exe"; IconFilename: "{app}\icon.png"
Name: "{autodesktop}\TaggJack"; Filename: "{app}\TaggJack.exe"; IconFilename: "{app}\icon.png"
```

### MSI Integration

For WiX/MSI installers, reference icon:
```xml
<Icon Id="icon.ico" SourceFile="icon.ico"/>
<Property Id="ARPPRODUCTICON" Value="icon.ico" />
```

## File Checklist

After icon integration, verify these files exist:

- ✅ `icon.png` - Source PNG image
- ✅ `icon.ico` - Converted ICO file (4 sizes)
- ✅ `convert-icon.ps1` - Conversion script
- ✅ `TaggJack.csproj` - Updated with icon references
- ✅ `Services/SystemTrayService.cs` - Updated to use icon.png
- ✅ `UI/AboutWindow.xaml` - Updated to use icon.png

## Best Practices

1. **Version control**: Commit both `icon.png` and `icon.ico` to Git
2. **Consistency**: Use same icon for all branding
3. **Quality**: Start with high-resolution PNG (256×256+)
4. **Testing**: Test icons on different backgrounds and DPI settings
5. **Documentation**: Note icon changes in changelog

## Related Files

- `Logo.png` - Splash screen logo (can be different from icon)
- `UI/SplashScreen.xaml` - Uses Logo.png for startup screen
- `Services/SystemTrayService.cs` - System tray icon loader
- `TaggJack.csproj` - Project configuration

## Summary

Your `icon.png` now serves as the unified icon source for TaggJack, providing:
- Professional branding across Windows UI elements
- Consistent visual identity
- High-quality display in both application and system tray
- Easy updates via PNG replacement and conversion script

The icon appears everywhere users interact with TaggJack! 🎨
