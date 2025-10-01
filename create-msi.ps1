# EvoDesigner Advanced Installer Script
# Creates MSI installer using WiX Toolset

param(
    [string]$Version = "1.0.0",
    [string]$OutputDir = "installer"
)

Write-Host "===========================================" -ForegroundColor Green
Write-Host "EvoDesigner Advanced MSI Installer Script" -ForegroundColor Green
Write-Host "===========================================" -ForegroundColor Green
Write-Host ""

# Check if WiX is installed
$wixPath = Get-Command "heat.exe" -ErrorAction SilentlyContinue
if (-not $wixPath) {
    Write-Host "WiX Toolset not found!" -ForegroundColor Red
    Write-Host "Please install WiX Toolset v3.11+ from: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    Write-Host "Or use the simpler Inno Setup method instead." -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1: Building application..." -ForegroundColor Cyan
& dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "./publish" -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Step 2: Creating WiX source files..." -ForegroundColor Cyan

# Create directory structure
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Generate heat file for components
$heatArgs = @(
    "dir", "publish",
    "-cg", "EvoDesignerComponents",
    "-dr", "INSTALLFOLDER", 
    "-gg", "-sf", "-srd",
    "-out", "$OutputDir\Components.wxs"
)
& heat.exe @heatArgs

# Create main WiX file
$wixContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" 
           Name="EvoDesigner" 
           Language="1033" 
           Version="$Version" 
           Manufacturer="EvoDesigner" 
           UpgradeCode="{12345678-1234-1234-1234-123456789012}">
           
    <Package InstallerVersion="200" 
             Compressed="yes" 
             InstallScope="perMachine" 
             Platform="x64" />

    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <Feature Id="ProductFeature" Title="EvoDesigner" Level="1">
      <ComponentGroupRef Id="EvoDesignerComponents" />
      <ComponentRef Id="ApplicationShortcut" />
    </Feature>

    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFiles64Folder">
        <Directory Id="INSTALLFOLDER" Name="EvoDesigner" />
      </Directory>
      <Directory Id="ProgramMenuFolder">
        <Directory Id="ApplicationProgramsFolder" Name="EvoDesigner"/>
      </Directory>
    </Directory>

    <DirectoryRef Id="ApplicationProgramsFolder">
      <Component Id="ApplicationShortcut" Guid="{ABCDEFGH-1234-1234-1234-123456789012}">
        <Shortcut Id="ApplicationStartMenuShortcut"
                  Name="EvoDesigner"
                  Target="[INSTALLFOLDER]EvoDesigner.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall"/>
        <RegistryValue Root="HKCU" 
                       Key="Software\EvoDesigner\Installed" 
                       Name="installed" 
                       Type="integer" 
                       Value="1" 
                       KeyPath="yes"/>
      </Component>
    </DirectoryRef>
  </Product>
</Wix>
"@

$wixContent | Out-File -FilePath "$OutputDir\EvoDesigner.wxs" -Encoding UTF8

Write-Host "Step 3: Compiling installer..." -ForegroundColor Cyan

# Compile WiX files
$candleArgs = @(
    "-ext", "WixUIExtension",
    "-out", "$OutputDir\",
    "$OutputDir\EvoDesigner.wxs",
    "$OutputDir\Components.wxs"
)
& candle.exe @candleArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Candle compilation failed!" -ForegroundColor Red
    exit 1
}

# Link MSI
$lightArgs = @(
    "-ext", "WixUIExtension",
    "-ext", "WixUtilExtension",
    "-out", "$OutputDir\EvoDesigner-$Version.msi",
    "$OutputDir\EvoDesigner.wixobj",
    "$OutputDir\Components.wixobj"
)
& light.exe @lightArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "Light linking failed!" -ForegroundColor Red
    exit 1
}

Write-Host "===========================================" -ForegroundColor Green
Write-Host "MSI Installer created successfully!" -ForegroundColor Green
Write-Host "Location: $OutputDir\EvoDesigner-$Version.msi" -ForegroundColor Yellow
Write-Host "===========================================" -ForegroundColor Green