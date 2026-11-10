# TaggJack - Installation and Deployment Guide

## 📦 Ready-to-Deploy Package

Your TaggJack application has been successfully compiled and packaged! 

### ✅ What's Been Created

1. **Self-Contained Executable** (`publish/TaggJack.exe` - 168MB)
   - Includes .NET 9.0 runtime
   - No additional software installation required
   - Works on Windows 10/11 x64

2. **Professional Installer Scripts**
   - Inno Setup script (`TaggJack-Setup.iss`)
   - WiX MSI script (`create-msi.ps1`)
   - Automated build script (`build-installer.bat`)

## 🚀 Deployment Options

### Option 1: Portable Application (Recommended for Quick Distribution)

**Advantages:**
- ✅ No installation required
- ✅ Can run from USB drive
- ✅ No registry changes
- ✅ Easy to deploy

**Steps:**
1. Copy the entire `publish/` folder to target computer
2. Run `TaggJack.exe`
3. Application starts immediately!

**File Size:** ~168MB (includes everything needed)

### Option 2: Professional Installer (Recommended for Business Use)

**A. Using Inno Setup (Free)**

1. **Download Inno Setup:**
   - Visit: https://jrsoftware.org/isinfo.php
   - Download and install Inno Setup 6.x

2. **Create Installer:**
   - Open `TaggJack-Setup.iss` with Inno Setup
   - Click "Build" menu → "Compile"
   - Installer will be created in `installer/` folder

3. **Features:**
   - Professional installation wizard
   - Start menu shortcuts
   - Desktop icon (optional)
   - Uninstaller
   - File association for .evotpl files
   - Registry integration

**B. Using WiX Toolset (Advanced)**

1. **Install WiX Toolset:**
   - Visit: https://wixtoolset.org/releases/
   - Download WiX v3.11+

2. **Create MSI:**
   ```powershell
   .\create-msi.ps1
   ```

3. **Features:**
   - Enterprise-grade MSI installer
   - Group Policy deployment support
   - Windows Installer compliance

## 🛠️ Build Process (For Developers)

To rebuild the application:

```batch
# Simple rebuild
.\build-installer.bat

# Or manual steps:
dotnet build --configuration Release
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "./publish"
```

## 📋 System Requirements

**Minimum Requirements:**
- Windows 10 x64 (version 1809 or later)
- Windows 11 x64
- 4GB RAM (8GB recommended)
- 200MB disk space
- .NET 9.0 runtime (included in self-contained build)

**Supported Features:**
- MySQL database connectivity
- Barcode generation (EAN-13, EAN-8)
- QR code generation
- Print preview and printing
- Label template management
- Drawing tools (lines, rectangles)
- Text formatting with custom fonts

## 🎯 Quick Start for End Users

1. **Portable Version:**
   - Extract files to desired location
   - Double-click `TaggJack.exe`
   - Start designing labels!

2. **Installed Version:**
   - Run the installer
   - Follow the wizard
   - Launch from Start Menu

## 🔧 Troubleshooting

**Application Won't Start:**
- Ensure Windows 10/11 x64
- Check antivirus isn't blocking the executable
- Try "Run as Administrator"

**MySQL Connection Issues:**
- Verify MySQL server is running
- Check connection string in application
- Ensure MySQL port (3306) is accessible

**Printing Problems:**
- Install correct printer drivers
- Check printer is set as default
- Verify paper size settings

## 📁 File Locations

**Application Files:**
- Portable: `[extract-location]/`
- Installed: `C:\Program Files\TaggJack\`

**Template Files:**
- Created templates: Same folder as executable
- File extension: `.evotpl`

**Application Data:**
- Debug logs: `debug.log` (if exists)
- Configuration: Stored in application folder

## 🎉 Features Overview

**TaggJack v1.0.0 includes:**

✅ **Label Design Tools**
- Drag & drop text elements
- Drawing tools (lines, rectangles)
- Resizable elements with visual handles
- Rotation support
- Professional color picker with HSV wheel

✅ **Typography & Styling**
- Custom font family selection
- Font size control
- Bold/italic styling
- Color customization
- Text alignment options

✅ **Barcode Support**
- EAN-13 barcode generation
- EAN-8 barcode generation
- QR code generation
- ZXing.Net integration

✅ **Data Integration**
- MySQL database connectivity
- Dynamic field binding
- Template-based data population

✅ **Print Management**
- Print preview
- Professional printing
- Label size customization
- Margin controls

✅ **Template System**
- Save/load templates as XML
- Template persistence
- Cross-session compatibility

## 📞 Support

For issues or questions:
- GitHub Issues: https://github.com/ehtearena/TaggJack/issues
- Project Repository: https://github.com/ehtearena/TaggJack

---

**Deployment Package Created:** September 18, 2025
**Application Version:** 1.0.0
**Target Framework:** .NET 9.0
**Platform:** Windows x64