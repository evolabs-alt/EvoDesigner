@echo off
REM ==========================================
REM EvoDesigner v1.1.0 Complete Build Script
REM ==========================================

echo.
echo ==========================================
echo EvoDesigner v1.1.0 - Complete Build
echo ==========================================
echo.

REM Step 1: Clean
echo [1/5] Cleaning previous builds...
dotnet clean
if exist "publish" rmdir /s /q "publish"
if exist "installer" rmdir /s /q "installer"
if exist "EvoDesigner-v1.1.0-Portable.zip" del /q "EvoDesigner-v1.1.0-Portable.zip"
echo Done.
echo.

REM Step 2: Build Release
echo [2/5] Building Release version...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Step 3: Publish
echo [3/5] Publishing self-contained application...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "./publish" -p:PublishSingleFile=true -p:PublishReadyToRun=true
if %errorlevel% neq 0 (
    echo ERROR: Publish failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Step 4: Create Portable ZIP
echo [4/5] Creating portable ZIP package...
powershell -Command "Compress-Archive -Path './publish/*' -DestinationPath './EvoDesigner-v1.1.0-Portable.zip' -Force"
if %errorlevel% neq 0 (
    echo ERROR: ZIP creation failed!
    pause
    exit /b 1
)
echo Done.
echo.

REM Step 5: Display Results
echo [5/5] Build Summary:
echo.
echo ✓ Application published to: %CD%\publish\
echo ✓ Main executable: EvoDesigner.exe
echo.
dir /b publish\EvoDesigner.exe 2>nul
if %errorlevel% equ 0 (
    for %%A in ("publish\EvoDesigner.exe") do echo   Size: %%~zA bytes
)
echo.
echo ✓ Portable package created: EvoDesigner-v1.1.0-Portable.zip
dir /b EvoDesigner-v1.1.0-Portable.zip 2>nul
if %errorlevel% equ 0 (
    for %%A in ("EvoDesigner-v1.1.0-Portable.zip") do echo   Size: %%~zA bytes
)
echo.
echo ==========================================
echo Distribution Options:
echo ==========================================
echo.
echo Option 1: Portable Version (READY)
echo   File: EvoDesigner-v1.1.0-Portable.zip
echo   Use: Extract and run EvoDesigner.exe
echo.
echo Option 2: Windows Installer (Optional)
echo   To create installer:
echo   1. Install Inno Setup from: https://jrsoftware.org/isinfo.php
echo   2. Open EvoDesigner-Setup.iss with Inno Setup
echo   3. Click Build -^> Compile
echo   4. Installer will be in: %CD%\installer\
echo.
echo ==========================================
echo ✓ BUILD COMPLETE!
echo ==========================================
echo.
echo You can now distribute:
echo - EvoDesigner-v1.1.0-Portable.zip
echo   OR
echo - Build installer using Inno Setup
echo.
echo See DEPLOYMENT-SUMMARY.md for full deployment guide.
echo.
pause
