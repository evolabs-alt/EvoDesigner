# EvoDesigner v1.1.0 - Distribution Guide

## 📦 Publication Summary

**Date:** October 11, 2025  
**Version:** 1.1.0  
**Build Type:** Self-contained Windows x64 application  
**Size:** ~212 MB (includes .NET 9 runtime)

## ✨ What's New in v1.1.0

### Major Features Added:
1. **Data Field Mapping for All Elements**
   - QR Codes, Barcodes (all types), and DataMatrix elements now have data binding dropdowns
   - Consistent UI across all data-bindable elements

2. **Printer Fallback System**
   - Automatic fallback to default printer when specified printer not found
   - Clear indication in logs showing which printer was requested vs. used
   - Format: `Adobe PDF (WC_MP 1 not found)`

3. **Enhanced Scheduled Printing**
   - Fixed bug where records were skipped instead of printing to fallback printer
   - Smart log consolidation for consecutive "No records" entries
   - Check count tracking showing how many times scheduler checked for records
   - Sample Data preview window (Data → Sample Data)

4. **Print Log Enhancements**
   - Double-click error logs to see detailed error messages
   - UPDATE query execution after successful prints
   - Error tracking for failed database updates

5. **User Experience Improvements**
   - Exit confirmation dialog
   - About window with version information
   - System tray icon with quick access menu
   - Improved keyboard shortcuts

## 📁 Distribution Options

### Option 1: Portable Distribution (Recommended for Quick Testing)

**Location:** `E:\Repos\EvoDesigner\publish\`

**Contents:**
- `EvoDesigner.exe` (212 MB) - Main executable with embedded .NET runtime
- `config.json` - Application configuration
- `Logo.png` - Application logo
- `icon.png` - Application icon
- DLL files (required dependencies)

**How to Use:**
1. Copy the entire `publish` folder to any location
2. Run `EvoDesigner.exe` - no installation needed!
3. All user data (templates, settings) stored in user's AppData folder

**Pros:**
- ✅ No installation required
- ✅ Works from USB drives
- ✅ Easy to test
- ✅ Multiple versions can coexist

**Cons:**
- ❌ No Start Menu integration
- ❌ No file association (.evotpl files)
- ❌ No automatic updates

### Option 2: Windows Installer (Recommended for End Users)

**Installer:** Creates MSI/EXE setup file using Inno Setup

**How to Create:**

#### Prerequisites:
1. Install Inno Setup from: https://jrsoftware.org/isinfo.php

#### Build Steps:
```bash
# Option A: Automatic build (if Inno Setup CLI is in PATH)
cd E:\Repos\EvoDesigner
.\build-installer.bat

# Option B: Manual build
1. Open Inno Setup
2. File → Open → Select "EvoDesigner-Setup.iss"
3. Build → Compile
```

**Output Location:** `E:\Repos\EvoDesigner\installer\EvoDesigner-Setup-v1.1.0.exe`

**Installer Features:**
- ✅ Start Menu shortcuts
- ✅ Desktop icon (optional)
- ✅ File association for .evotpl files
- ✅ Automatic uninstaller
- ✅ Proper Program Files installation
- ✅ Registry integration
- ✅ Professional user experience

**Distribution:**
- Share the single `EvoDesigner-Setup-v1.1.0.exe` file (~210 MB)
- Users double-click to install
- Installs to: `C:\Program Files\EvoDesigner\`

## 🚀 Quick Start for End Users

### Portable Version:
```
1. Extract the publish folder
2. Double-click EvoDesigner.exe
3. Start designing labels!
```

### Installed Version:
```
1. Run EvoDesigner-Setup-v1.1.0.exe
2. Follow installation wizard
3. Launch from Start Menu
```

## 🔧 System Requirements

**Operating System:**
- Windows 10 (64-bit) or later
- Windows 11 (64-bit)

**Hardware:**
- Processor: x64 compatible CPU
- RAM: 512 MB minimum, 1 GB recommended
- Disk Space: 300 MB for installation
- Display: 1024x768 minimum resolution

**Software:**
- No .NET installation required (self-contained)
- MySQL Server (if using database features)
- Printer drivers (for printing functionality)

## 📊 File Structure

### Installed Application:
```
C:\Program Files\EvoDesigner\
├── EvoDesigner.exe          (Main application)
├── config.json              (Global configuration)
├── Logo.png                 (Application logo)
├── icon.png                 (Application icon)
└── [DLL files]              (Dependencies)
```

### User Data:
```
%APPDATA%\EvoDesigner\
├── templates\               (User-created label templates)
├── logs\                    (Application logs)
└── settings.json            (User preferences)
```

## 🔐 Configuration

### Initial Setup:
1. Launch EvoDesigner
2. Configure database connection (if needed):
   - Menu: Data → Configure Data Source
   - Enter MySQL connection details
   - Test connection

3. Set up scheduled printing (if needed):
   - Menu: File → Scheduled Printing
   - Configure templates and intervals

### config.json Structure:
```json
{
  "DefaultPrinter": "Adobe PDF",
  "AutoSave": true,
  "CheckForUpdates": true,
  "LogLevel": "Info"
}
```

## 🛠️ Troubleshooting

### Application Won't Start:
- **Issue:** EvoDesigner.exe doesn't launch
- **Solution:** 
  - Check Windows Event Viewer for errors
  - Run as Administrator
  - Verify all DLL files are present

### Database Connection Failed:
- **Issue:** Cannot connect to MySQL
- **Solution:**
  - Verify MySQL server is running
  - Check connection string in Data Source config
  - Ensure firewall allows MySQL port (3306)

### Printer Not Found:
- **Issue:** Selected printer not available
- **Solution:**
  - Application automatically falls back to default printer
  - Check printer is installed in Windows
  - Check Logs window for printer fallback messages

### Template Won't Load:
- **Issue:** .evotpl file doesn't open
- **Solution:**
  - Verify file is valid XML
  - Check file permissions
  - Try File → Open instead of double-clicking

## 📝 Release Notes

### Version 1.1.0 (October 11, 2025)

**New Features:**
- ✨ Data field mapping for QR codes and all barcode types
- ✨ Printer fallback system with clear logging
- ✨ Sample Data preview window
- ✨ Enhanced scheduled printing with smart log consolidation
- ✨ UPDATE query execution after prints
- ✨ Error popup on log record double-click

**Bug Fixes:**
- 🐛 Fixed records being skipped instead of using fallback printer
- 🐛 Fixed text alignment not showing in print preview
- 🐛 Fixed blank labels printing when no records found

**Improvements:**
- 🎨 Exit confirmation dialog
- 🎨 About window with version info
- 🎨 System tray integration
- 🎨 Check count display in logs
- 🎨 Consistent element properties UI

### Version 1.0.0 (Initial Release)

**Core Features:**
- Label designer with drag-and-drop interface
- Support for Text, Images, Barcodes, QR Codes, Shapes
- MySQL data binding
- Print preview and printing
- Template save/load as XML
- Scheduled printing

## 📞 Support

**Documentation:**
- README.md - Getting started guide
- Feature documentation in project folder

**Issues:**
- Report bugs on GitHub Issues
- Feature requests welcome

**Contact:**
- Company: Evolabs IT Solutions Ltd
- Copyright: © 2025 Evolabs IT Solutions Ltd

## 🔄 Future Updates

**Planned Features:**
- Cloud template storage
- Multi-language support
- Advanced barcode symbologies
- Template marketplace
- Batch printing enhancements

## 📜 License

See LICENSE file for licensing information.

## 🙏 Acknowledgments

Built with:
- .NET 9.0
- WPF (Windows Presentation Foundation)
- ZXing.Net (Barcode generation)
- QRCoder (QR code generation)
- MySQL.Data (Database connectivity)
- Material Icons WPF (UI icons)

---

**Ready to Deploy! 🚀**

Choose your distribution method and share EvoDesigner with your users!
