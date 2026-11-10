# Splash Screen Feature

## Overview
Added a professional splash screen that displays the TaggJack logo and loading status during application startup.

## Visual Design

### Layout
- **Size**: 600x400 pixels
- **Style**: Borderless window with rounded corners
- **Background**: White with blue border (#2196F3)
- **Position**: Center screen
- **Transparency**: Enabled for smooth rounded corners

### Components

1. **Logo Display**
   - Large centered logo image (`/Logo.png`)
   - High-quality bitmap scaling
   - Fills most of the splash screen area
   - 60-pixel margin for breathing room

2. **Application Name**
   - "TaggJack" in bold 32pt font
   - Blue color (#2196F3) matching brand
   - Centered below logo

3. **Loading Indicator**
   - Animated progress bar (indeterminate)
   - Blue (#2196F3) with light gray background (#E0E0E0)
   - 300 pixels wide, 6 pixels tall

4. **Status Text**
   - Dynamic loading messages
   - 14pt gray text (#666666)
   - Updates as startup progresses

## Startup Flow

The splash screen displays these status messages during startup:

1. **"Loading..."** - Initial display
2. **"Loading configuration..."** - Reading app config
3. **"Initializing flow mode..."** - Starting flow mode (if enabled)
4. **"Initializing services..."** - Creating service instances
5. **"Initializing system tray..."** - Setting up tray icon
6. **"Starting scheduler..."** - Starting scheduled printing
7. **"Flow mode ready!"** - Flow mode complete
8. **"Running tests..."** - Executing startup tests
9. **"Starting application..."** - Final stage before close

## Technical Implementation

### Files Created

**UI/SplashScreen.xaml**
- XAML layout with:
  - Transparent borderless window
  - Rounded border with shadow effect
  - 3-row grid layout (Logo, Title, Loading)
  - High-quality image rendering

**UI/SplashScreen.xaml.cs**
- Code-behind with methods:
  - `ShowSplash()` - Displays splash with minimum time
  - `UpdateStatus(string)` - Updates status text
  - `CloseSplash()` - Closes after minimum display time
  - Minimum display time: 2000ms (2 seconds)

### Integration in App.xaml.cs

Modified `OnStartup()` method:
```csharp
// Show splash screen
_splashScreen = new UI.SplashScreen();
_splashScreen.ShowSplash();

// Update status during initialization
_splashScreen.UpdateStatus("Loading configuration...");
// ... more status updates ...

// Close splash screen
_splashScreen.UpdateStatus("Starting application...");
Task.Delay(500).ContinueWith(_ =>
{
    Dispatcher.Invoke(() =>
    {
        _splashScreen.CloseSplash();
    });
});
```

### Minimum Display Time

The splash screen enforces a minimum display time of 2 seconds to ensure:
- Users can see the logo and branding
- Prevents flickering on fast systems
- Professional startup experience
- Smooth transition to main app

Even if initialization completes faster, splash remains visible for the minimum time.

## User Experience

### Fast Systems
- Splash shows for exactly 2 seconds minimum
- Clean, professional presentation
- No flickering or abrupt transitions

### Slow Systems
- Splash shows until initialization completes
- Status updates provide feedback
- Users know app is loading, not frozen

### Flow Mode (System Tray)
- Splash appears even in tray-only mode
- Shows initialization progress
- Closes after services start
- Tray icon ready when splash closes

## Customization Options

### Adjusting Display Time

Edit `UI/SplashScreen.xaml.cs`:
```csharp
private int _minimumDisplayTime = 2000; // Change to desired milliseconds
```

### Changing Colors

Edit `UI/SplashScreen.xaml`:
```xml
<!-- Border color -->
BorderBrush="#2196F3"

<!-- Progress bar color -->
Foreground="#2196F3"

<!-- Status text color -->
Foreground="#666666"
```

### Changing Size

Edit `UI/SplashScreen.xaml`:
```xml
Height="400"  <!-- Change height -->
Width="600"   <!-- Change width -->
```

### Using Different Logo

Replace `/Logo.png` with your image or update source:
```xml
<Image Source="/YourLogo.png" />
```

## Benefits

1. **Professional Appearance**
   - Branded startup experience
   - Polished, modern design
   - Matches application theme

2. **User Feedback**
   - Clear loading indicators
   - Status messages show progress
   - Users know app is working

3. **Branding**
   - Logo prominently displayed
   - Reinforces brand identity
   - Memorable first impression

4. **Perceived Performance**
   - App feels responsive
   - Loading time feels shorter
   - Better than blank screen

## Future Enhancements

Possible improvements:
1. **Version number display** - Show app version on splash
2. **Fade in/out animations** - Smooth entrance and exit
3. **Progress percentage** - Show actual progress bar
4. **Configurable timing** - Allow users to set display time
5. **Theme support** - Dark/light splash screen themes
6. **Multiple images** - Rotate between different splash screens

## Testing

To test the splash screen:
1. Launch the application
2. Observe splash screen appears immediately
3. Watch status messages update
4. Verify minimum 2-second display
5. Confirm smooth transition to main app/tray
6. Test on both fast and slow systems

The splash screen should always display cleanly with the logo centered and status updates visible.
