# Scheduler Control Feature

## Overview
Added Start/Stop scheduler functionality to the system tray menu, allowing users to control the scheduled printing service at runtime.

## Problem Solved
Previously, the scheduler automatically started when the application launched and could not be stopped from the UI. The menu showed a status but no control mechanism.

## Implementation

### Features Added

1. **Dynamic Menu Items**
   - When scheduler is **Active**: Shows "Stop Scheduler" button
   - When scheduler is **Stopped**: Shows "Start Scheduler" button
   - Status indicator shows: "Status: Active" or "Status: Stopped"

2. **Start Scheduler**
   - Starts the scheduled printing timer
   - Shows balloon notification: "Scheduler started successfully"
   - Refreshes menu to show "Stop Scheduler" button
   - Status updates to "Active"

3. **Stop Scheduler**
   - Stops the scheduled printing timer
   - Shows balloon notification: "Scheduler stopped successfully"
   - Refreshes menu to show "Start Scheduler" button
   - Status updates to "Stopped"

### Menu Structure

```
- Open Designer (bold)
---
- Add Template...
- Loaded Templates
- Remove Template
---
- Run Now
- Stop Scheduler (if active) / Start Scheduler (if stopped)
- Status: Active / Stopped (disabled, read-only)
---
- Exit
```

### Code Changes

**Services/SystemTrayService.cs**

1. **Updated CreateContextMenu()** (lines 154-177)
   - Added conditional rendering of Start/Stop buttons based on `IsSchedulerActive`
   - Changed status item to read-only with "Status:" prefix

2. **Added StartScheduler()** (lines 429-447)
   - Calls `_scheduledPrinting.Start()`
   - Shows success balloon notification
   - Calls `RefreshContextMenu()` to update menu

3. **Added StopScheduler()** (lines 449-467)
   - Calls `_scheduledPrinting.Stop()`
   - Shows success balloon notification
   - Calls `RefreshContextMenu()` to update menu

### Menu Refresh Mechanism

The menu automatically refreshes when:
1. **Before opening**: `TrayContextMenuOpen` event → `RefreshContextMenu()`
2. **After starting scheduler**: `StartScheduler()` → `RefreshContextMenu()`
3. **After stopping scheduler**: `StopScheduler()` → `RefreshContextMenu()`
4. **When template added/removed**: Event handlers → `RefreshContextMenu()`

This ensures the menu always shows the current scheduler state.

## User Experience

### Starting the Scheduler
1. Right-click tray icon
2. Click "Start Scheduler"
3. Balloon notification appears: "Scheduler started successfully"
4. Menu now shows "Stop Scheduler" and "Status: Active"
5. Scheduler runs print jobs every 60 seconds

### Stopping the Scheduler
1. Right-click tray icon
2. Click "Stop Scheduler"
3. Balloon notification appears: "Scheduler stopped successfully"
4. Menu now shows "Start Scheduler" and "Status: Stopped"
5. Scheduled printing stops (manual "Run Now" still works)

## Technical Details

### IsSchedulerActive Property
- Located in `ScheduledPrintingService.cs`
- Returns: `_timer != null`
- Tracks whether the timer object exists (scheduler is running)
- Different from `IsRunning` which tracks active print cycles

### Error Handling
- Both Start/Stop methods include try-catch blocks
- Errors shown via MessageBox dialog
- Prevents application crashes from scheduler control failures

## Default Behavior

By default (from `Models/AppConfig.cs`):
```csharp
public bool ScheduledPrintingEnabled { get; set; } = true;
```

- Scheduler **starts automatically** when application launches
- Menu initially shows "Stop Scheduler" and "Status: Active"
- User can manually stop/start as needed
- Setting persists until application restart

## Future Enhancements

Possible improvements:
1. **Persist scheduler state**: Save user preference to config file
2. **Keyboard shortcuts**: Add hotkeys for Start/Stop
3. **Custom intervals**: Allow users to change print interval from menu
4. **Scheduler statistics**: Show last run time, next scheduled time
5. **Confirmation dialogs**: Ask before stopping scheduler if jobs are pending

## Testing

To test the feature:
1. Launch application (scheduler starts automatically)
2. Right-click tray icon → Verify "Stop Scheduler" and "Status: Active"
3. Click "Stop Scheduler" → Verify balloon notification
4. Right-click again → Verify "Start Scheduler" and "Status: Stopped"
5. Click "Start Scheduler" → Verify balloon notification
6. Right-click again → Verify "Stop Scheduler" and "Status: Active"
7. Verify scheduled printing works when Active, stops when Stopped

## Related Files

- `Services/SystemTrayService.cs` - Menu and control logic
- `Services/ScheduledPrintingService.cs` - Scheduler implementation
- `Models/AppConfig.cs` - Configuration defaults
- `App.xaml.cs` - Application startup and scheduler initialization
