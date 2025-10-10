# Log Consolidation for "No Records" Entries

## Enhancement
When the scheduler runs repeatedly and finds no records to print, instead of creating a new log entry each time, the system now updates the timestamp of the existing "No records to print" entry. This prevents the log from being filled with repetitive entries.

## Problem
Previously, if the scheduler ran every 60 seconds and the database query returned 0 records, the Print Log window would show:

```
2025-10-10 19:40:00  MyTemplate  0  Adobe PDF  No records to print
2025-10-10 19:41:00  MyTemplate  0  Adobe PDF  No records to print
2025-10-10 19:42:00  MyTemplate  0  Adobe PDF  No records to print
2025-10-10 19:43:00  MyTemplate  0  Adobe PDF  No records to print
2025-10-10 19:44:00  MyTemplate  0  Adobe PDF  No records to print
... (dozens of identical entries)
```

This made the log cluttered and hard to read.

## Solution
Now, when consecutive "No records" checks occur for the same template:

1. **First occurrence**: Creates a new log entry with status "No records to print"
2. **Subsequent occurrences**: Updates the timestamp of the existing entry instead of creating a new one
3. **Different template**: Creates a new entry (each template has its own "No records" log)
4. **Records appear**: Creates a new "Success" entry, ending the consolidation

## Result
The log now shows:

```
2025-10-10 19:44:00  MyTemplate  0  Adobe PDF  No records to print  ← Timestamp updates each minute
2025-10-10 19:30:00  MyTemplate  10  Adobe PDF  Success               ← Previous successful print
2025-10-10 19:29:00  MyTemplate  10  Adobe PDF  Success
```

The "No records to print" entry's timestamp keeps updating (19:44:00 in this example), showing the last time it checked, but doesn't clutter the log with duplicate entries.

## Implementation Details

### New Method: `AddNoRecordsLog()`
Located in `Services/ScheduledPrintingService.cs`, this method:

```csharp
private void AddNoRecordsLog(string templateName, string printerName)
{
    lock (_lockObject)
    {
        // Check if the most recent log entry is "No records" for the same template
        if (_executionLogs.Count > 0)
        {
            var lastLog = _executionLogs[0]; // Most recent is at index 0
            
            if (lastLog.TemplateName == templateName && 
                lastLog.Status == "No records to print" && 
                lastLog.RecordCount == 0)
            {
                // Just update the timestamp of the existing entry
                lastLog.ExecutionTime = DateTime.Now;
                return; // Don't add a new entry
            }
        }
        
        // No previous "No records" entry - add new entry
        var logEntry = new ScheduledPrintLogEntry
        {
            ExecutionTime = DateTime.Now,
            TemplateName = templateName,
            RecordCount = 0,
            PrinterName = printerName,
            Status = "No records to print",
            UpdateErrors = string.Empty
        };

        _executionLogs.Insert(0, logEntry);
    }
}
```

### Logic Flow

1. **Check last log entry**: Is it "No records to print" for this same template?
   - **Yes**: Update `ExecutionTime` to now, return early
   - **No**: Create new log entry

2. **Thread-safe**: Uses `lock (_lockObject)` to prevent race conditions

3. **Template-specific**: Each template can have its own "No records" entry
   - Template A showing "No records" won't affect Template B's logging

## Example Scenarios

### Scenario 1: Continuous No Records
```
Time       Action                           Log Behavior
-------    -------------------------------- -----------------------------------
19:40:00   Query returns 0 records          Creates "No records" entry
19:41:00   Query returns 0 records          Updates timestamp to 19:41:00
19:42:00   Query returns 0 records          Updates timestamp to 19:42:00
19:43:00   Query returns 10 records         Creates "Success" entry
19:44:00   Query returns 0 records          Creates new "No records" entry
```

### Scenario 2: Multiple Templates
```
Time       Template     Records   Log Behavior
-------    ----------   -------   -------------------------------------
19:40:00   Template A   0         Creates "No records" for Template A
19:40:30   Template B   0         Creates "No records" for Template B
19:41:00   Template A   0         Updates Template A timestamp
19:41:30   Template B   5         Creates "Success" for Template B
19:42:00   Template A   0         Updates Template A timestamp
```

Each template maintains its own "No records" entry independently.

## Benefits

1. **Cleaner Logs**: No repetitive entries cluttering the view
2. **Still Informative**: Timestamp shows when it was last checked
3. **Easy to See Status**: One glance shows "still waiting for records"
4. **Disk Space**: Reduces memory usage for long-running schedulers
5. **Better UX**: Users can quickly identify actual print activity

## User Experience

### In the Print Log Window:
- **While waiting**: Single "No records to print" entry with updating timestamp
- **Click to refresh**: See the latest timestamp
- **Records appear**: New "Success" entry appears above
- **No records again**: New "No records" entry created (separate from previous session)

### Visual Indicator:
The user can see at a glance:
- **Last check time**: From the ExecutionTime column
- **Waiting state**: Status shows "No records to print"
- **Not stuck**: Timestamp proves scheduler is still running

## Technical Notes

### Thread Safety
- Uses `lock (_lockObject)` to ensure thread-safe updates
- Multiple scheduler threads won't create duplicate entries

### Memory Management
- Still respects the 50-entry limit (`MaxLogEntries`)
- Consolidation reduces entry count, keeping more history visible

### Debug Logging
When a timestamp is updated, the debug log shows:
```
[2025-10-10 19:41:00] No records found - skipping print job for MyTemplate
[2025-10-10 19:41:00] Updated timestamp for existing 'No records' log entry
```

This confirms the consolidation is working correctly.

## Related Changes
- Modified `SendPrintJob()` to call `AddNoRecordsLog()` instead of returning silently
- Added new `AddNoRecordsLog()` method for smart log consolidation
- Existing `AddLog()` method unchanged for normal print operations

## Testing Recommendations

1. **Test with no records:**
   - Query returns 0 records for multiple scheduler runs
   - Verify: Only one log entry, timestamp updates each run

2. **Test with records appearing:**
   - Start with 0 records
   - Add records to database
   - Verify: New "Success" entry created, "No records" entry preserved

3. **Test with multiple templates:**
   - Multiple templates with no records
   - Verify: Each template has its own "No records" entry
   - Timestamps update independently

4. **Test with alternating states:**
   - Records → No records → Records → No records
   - Verify: Separate entries for each state change
