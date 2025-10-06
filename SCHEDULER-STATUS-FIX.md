# Scheduler Status Fix

## Issue
The **"Scheduler Status"** menu item in the system tray was showing incorrect status:
- Showed "Running" only during active print cycles
- Showed "Stopped" even when scheduler timer was active

## Root Cause
The menu was checking `IsRunning` property which indicates:
- ✅ `true` = Currently executing a print cycle
- ❌ `false` = Waiting for next cycle (but scheduler could still be active!)

## Solution
Added new `IsSchedulerActive` property to track the actual scheduler state:

```csharp
public bool IsSchedulerActive => _timer != null;
```

This property returns:
- `true` when the timer is started (scheduled printing enabled)
- `false` when the timer is stopped/disposed

## Behavior Now

### Scheduler: Active
Displayed when:
- Application starts in flow mode with `ScheduledPrintingEnabled = true`
- Scheduler timer is running (printing every 60 seconds)
- **Shows "Active" even between print cycles**

### Scheduler: Stopped
Displayed when:
- Flow mode disabled
- Scheduled printing manually stopped
- Timer not initialized

## Menu Display

**Before Fix:**
```
Run Now
Scheduler: Running      ← Only showed during print
Scheduler: Stopped      ← Showed rest of the time (confusing!)
```

**After Fix:**
```
Run Now
Scheduler: Active       ← Shows when timer is active
Scheduler: Stopped      ← Shows when truly stopped
```

## Technical Changes

### ScheduledPrintingService.cs
```csharp
// Added property
public bool IsSchedulerActive => _timer != null;

// Existing (unchanged)
public bool IsRunning => _isRunning;  // True only during print cycle
```

### SystemTrayService.cs
```csharp
// Changed from:
Header = $"Scheduler: {(_scheduledPrinting.IsRunning ? "Running" : "Stopped")}"

// To:
Header = $"Scheduler: {(_scheduledPrinting.IsSchedulerActive ? "Active" : "Stopped")}"
```

## Property Meanings

| Property | Meaning | Use Case |
|----------|---------|----------|
| `IsRunning` | Print cycle actively executing | Show "Printing..." status |
| `IsSchedulerActive` | Timer started, scheduler enabled | Show "Active" in menu |

## Testing

1. **Start app in flow mode:**
   - ✅ Menu shows "Scheduler: Active"
   
2. **Wait between print cycles:**
   - ✅ Still shows "Scheduler: Active" (not "Stopped")
   
3. **During actual printing:**
   - ✅ Still shows "Scheduler: Active"
   - ✅ `IsRunning` = true internally
   
4. **Stop scheduler:**
   - ✅ Menu shows "Scheduler: Stopped"

## Status: ✅ Fixed

The scheduler status now correctly reflects whether the scheduled printing timer is active, not just whether it's currently in the middle of a print cycle.
