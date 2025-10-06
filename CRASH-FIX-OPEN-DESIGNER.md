# Fix: Crash When Clicking "Open Designer" While Window Already Open

## Problem
The application crashed when the user clicked "Open Designer" from the system tray menu while the designer window was already open and visible.

## Root Cause
The `ShowMainWindow()` method in `SystemTrayService.cs` was attempting to call `Show()` on a window that was already visible. This caused undefined behavior and could lead to crashes, particularly when:
1. The window was already visible and active
2. The window state was being manipulated while already in that state
3. Multiple rapid clicks occurred

## Solution
Modified the `ShowMainWindow()` method to:

1. **Check Window Visibility**: Before attempting to show the window, check if it's already visible using `IsVisible` property
2. **Conditional Actions**: 
   - If window is **already visible**: Only restore from minimized state if needed, then activate and focus
   - If window is **hidden**: Show it, set to normal state, and activate
3. **Improved Error Handling**: Added comprehensive try-catch with logging to prevent crashes and provide user feedback
4. **User-Friendly Messages**: Added informative message boxes for edge cases (e.g., window not yet created in flow mode)

## Changes Made

### File: `Services/SystemTrayService.cs`

**Method: `ShowMainWindow()`**

#### Before:
```csharp
private void ShowMainWindow()
{
    if (_mainWindow != null)
    {
        _mainWindow.Show();  // ❌ Called even if already visible
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
    // ... similar issues in fallback logic
}
```

#### After:
```csharp
private void ShowMainWindow()
{
    try
    {
        if (_mainWindow != null)
        {
            // ✅ Check if window is already visible
            if (_mainWindow.IsVisible)
            {
                // Just bring to front if already visible
                if (_mainWindow.WindowState == WindowState.Minimized)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                }
                _mainWindow.Activate();
                _mainWindow.Focus();
            }
            else
            {
                // Show the window if it's hidden
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
            return;
        }
        // ... similar improvements to fallback logic
    }
    catch (Exception ex)
    {
        // ✅ Comprehensive error handling
        File.AppendAllText("tray-debug.log", 
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in ShowMainWindow: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
        
        MessageBox.Show(
            $"An error occurred while trying to show the designer window: {ex.Message}",
            "TaggJack - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
```

## Benefits

1. **Prevents Crashes**: No longer attempts to show an already-visible window
2. **Better UX**: Smoothly brings window to front without flickering or state changes
3. **Robust Error Handling**: Catches and logs any unexpected errors with user-friendly messages
4. **Performance**: Avoids unnecessary window operations when window is already visible
5. **Debugging**: Logs errors to `tray-debug.log` for troubleshooting

## Testing Recommendations

1. Click "Open Designer" when window is visible - should bring to front
2. Hide window to tray, then click "Open Designer" - should show window
3. Minimize window, then click "Open Designer" - should restore and activate
4. Rapidly click "Open Designer" multiple times - should handle gracefully
5. Double-click tray icon (same functionality) - should work as expected

## Related Components

- `Services/SystemTrayService.cs` - Main fix location
- `UI/MainWindow.xaml.cs` - Window closing/hiding behavior in flow mode
- `App.xaml.cs` - Window initialization and reference management

## Date: 2025-10-06
