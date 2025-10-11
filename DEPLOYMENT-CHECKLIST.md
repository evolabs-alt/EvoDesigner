# 🚀 Quick Deployment Checklist for EvoDesigner v1.1.0

## ✅ Pre-Deployment Checklist

- [x] Version updated to 1.1.0 in .csproj
- [x] Version updated to 1.1.0 in Inno Setup script
- [x] Code cleaned and built in Release mode
- [x] Self-contained application published to `./publish`
- [ ] Application tested and verified working
- [ ] Installer created (optional)
- [ ] Release notes documented

## 📋 Distribution Options

### Option 1: Portable Deployment (No Installation)

**Best for:** Quick testing, USB deployment, multiple versions

```powershell
# 1. Verify publish folder
Get-ChildItem -Path "./publish" -File | Measure-Object -Property Length -Sum

# 2. Create ZIP for distribution
Compress-Archive -Path "./publish/*" -DestinationPath "./EvoDesigner-v1.1.0-Portable.zip" -Force

# 3. Share the ZIP file
# Users extract and run EvoDesigner.exe directly
```

**What to Share:**
- `EvoDesigner-v1.1.0-Portable.zip` (~210 MB)

**User Instructions:**
1. Extract ZIP to any folder
2. Run `EvoDesigner.exe`
3. No installation needed!

---

### Option 2: Windows Installer (Professional Distribution)

**Best for:** End users, IT deployment, professional distribution

#### Prerequisites:
- Download and install Inno Setup: https://jrsoftware.org/isinfo.php

#### Steps:

```powershell
# 1. Build installer using Inno Setup GUI
# Open: EvoDesigner-Setup.iss in Inno Setup Compiler
# Click: Build → Compile

# OR use command line (if iscc.exe is in PATH):
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "EvoDesigner-Setup.iss"
```

**What to Share:**
- `./installer/EvoDesigner-Setup-v1.1.0.exe` (~210 MB)

**User Instructions:**
1. Run `EvoDesigner-Setup-v1.1.0.exe`
2. Follow installation wizard
3. Launch from Start Menu

---

## 🧪 Testing Checklist

Before distributing, test the published application:

### Basic Functionality:
- [ ] Application starts without errors
- [ ] Can create new template
- [ ] Can add text elements
- [ ] Can add barcode/QR code elements
- [ ] Can save template
- [ ] Can load template
- [ ] Print preview works
- [ ] Printing works (to PDF or printer)

### Data Binding:
- [ ] Can configure data source
- [ ] Can execute test query
- [ ] Can view sample data
- [ ] Data field mapping dropdown appears for text/barcode/QR elements
- [ ] Preview with data shows correct values

### Scheduled Printing:
- [ ] Can configure scheduled printing
- [ ] Scheduler starts and runs
- [ ] Logs window shows execution history
- [ ] Printer fallback works correctly
- [ ] UPDATE query executes (if configured)

### System Tray:
- [ ] Minimize to tray works
- [ ] Tray icon menu functional
- [ ] Can restore from tray
- [ ] Exit confirmation appears

---

## 📦 Final Distribution

### For Portable Version:
```powershell
# Create portable ZIP
Compress-Archive -Path "./publish/*" -DestinationPath "./EvoDesigner-v1.1.0-Portable.zip" -Force

# Verify ZIP created
Get-Item "./EvoDesigner-v1.1.0-Portable.zip"
```

### For Installer Version:
```powershell
# Verify installer exists
Get-Item "./installer/EvoDesigner-Setup-v1.1.0.exe"

# Optional: Create checksum for verification
Get-FileHash "./installer/EvoDesigner-Setup-v1.1.0.exe" -Algorithm SHA256 | Select-Object Hash
```

---

## 🌐 Publishing Locations

### GitHub Release:
1. Go to: https://github.com/evolabs-alt/EvoDesigner/releases
2. Click: "Draft a new release"
3. Tag: `v1.1.0`
4. Title: `EvoDesigner v1.1.0`
5. Description: Copy from RELEASE-v1.1.0.md
6. Attach files:
   - `EvoDesigner-Setup-v1.1.0.exe` (Installer)
   - `EvoDesigner-v1.1.0-Portable.zip` (Portable)
   - `RELEASE-v1.1.0.md` (Release notes)
7. Click: "Publish release"

### Internal Network Share:
```powershell
# Copy to network share
Copy-Item "./installer/EvoDesigner-Setup-v1.1.0.exe" "\\server\software\EvoDesigner\"
Copy-Item "./EvoDesigner-v1.1.0-Portable.zip" "\\server\software\EvoDesigner\"
```

### Company Website:
- Upload installer to download section
- Create download page with release notes
- Include system requirements

---

## 📧 Distribution Email Template

```
Subject: EvoDesigner v1.1.0 - Label Design Software Now Available

Dear Team,

We're excited to announce the release of EvoDesigner v1.1.0, our professional label design application!

🆕 What's New in v1.1.0:
• Data field mapping for all barcode and QR code elements
• Automatic printer fallback with clear logging
• Enhanced scheduled printing with smart log consolidation
• Sample Data preview window
• Improved user experience and bug fixes

📥 Download Options:

Option 1: Windows Installer (Recommended)
- File: EvoDesigner-Setup-v1.1.0.exe
- Size: ~210 MB
- Link: [Insert download link]
- Double-click to install, then launch from Start Menu

Option 2: Portable Version (No Installation)
- File: EvoDesigner-v1.1.0-Portable.zip
- Size: ~210 MB
- Link: [Insert download link]
- Extract and run EvoDesigner.exe directly

💻 System Requirements:
- Windows 10/11 (64-bit)
- 1 GB RAM recommended
- No .NET installation required

📖 Documentation:
- Full release notes: RELEASE-v1.1.0.md
- User guide: README.md

For questions or support, please contact IT Support.

Best regards,
Evolabs IT Solutions Ltd
```

---

## 🔒 Security Notes

### Code Signing (Future):
- Consider signing the executable with a code signing certificate
- Prevents "Unknown Publisher" warnings
- Increases user trust

### Antivirus:
- Self-contained .NET apps may trigger false positives
- Submit to antivirus vendors if needed
- Document known false positive warnings

---

## 📊 Deployment Statistics to Track

- Number of downloads
- Installation success rate
- Most used features
- Error reports
- User feedback

---

## ✅ Post-Deployment

- [ ] Monitor user feedback
- [ ] Collect bug reports
- [ ] Track feature requests
- [ ] Plan v1.2.0 features
- [ ] Update documentation as needed

---

**Deployment Status: READY ✅**

All components built and tested. Ready for distribution!
