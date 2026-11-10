@echo off
echo ==========================================
echo TaggJack Build and Installer Script
echo ==========================================
echo.

echo Step 1: Cleaning previous builds...
if exist "publish" rmdir /s /q "publish"
if exist "installer" rmdir /s /q "installer"
echo Done.

echo.
echo Step 2: Building Release version...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)
echo Done.

echo.
echo Step 3: Publishing self-contained application...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "./publish" -p:PublishSingleFile=true -p:PublishReadyToRun=true
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)
echo Done.

echo.
echo Step 4: Application published successfully!
echo Location: %CD%\publish\
echo Main executable: TaggJack.exe
echo.

echo Step 5: Creating installer (requires Inno Setup)...
echo.
echo To create an installer:
echo 1. Install Inno Setup from: https://jrsoftware.org/isinfo.php
echo 2. Open TaggJack-Setup.iss with Inno Setup
echo 3. Click "Build" to create the installer
echo.
echo Alternative: Use the publish folder for portable deployment
echo.

dir /b publish\TaggJack.exe 2>nul
if %errorlevel% equ 0 (
    echo ✓ TaggJack.exe created successfully
) else (
    echo ✗ TaggJack.exe not found
)

echo.
echo ==========================================
echo Build completed successfully!
echo ==========================================
pause