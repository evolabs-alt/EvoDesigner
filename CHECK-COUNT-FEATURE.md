# Check Count Display for "No Records" Entries

## Enhancement
The "No records to print" status now displays a count in brackets showing how many times the scheduler has checked and found no records. This provides better visibility into scheduler activity.

## Visual Examples

### First Check (count = 1):
```
Status: No records to print
```
(No count shown for the first check)

### After 2nd Check (count = 2):
```
Status: No records to print (2)
```

### After 5th Check (count = 5):
```
Status: No records to print (5)
```

### After 10th Check (count = 10):
```
Status: No records to print (10)
```

## How It Works

### Initial State
When the scheduler first finds no records:
- Creates log entry with `NoRecordsCheckCount = 1`
- Status displays: `"No records to print"` (no count shown)

### Subsequent Checks
Each time the scheduler checks again and still finds no records:
- Increments `NoRecordsCheckCount` by 1
- Updates timestamp to current time
- Status displays: `"No records to print (N)"` where N ≥ 2

### When Records Appear
When records are finally found:
- New "Success" entry is created
- Count resets for next "No records" session

## Example Timeline

```
Time       Records   Action                         Log Display
---------- --------- ------------------------------ ----------------------------------
19:40:00   0         First check, no records        No records to print
19:41:00   0         2nd check, still no records    No records to print (2)
19:42:00   0         3rd check, still no records    No records to print (3)
19:43:00   0         4th check, still no records    No records to print (4)
19:44:00   0         5th check, still no records    No records to print (5)
19:45:00   10        Records found!                 Success
19:46:00   0         New "no records" session       No records to print
19:47:00   0         2nd check in new session       No records to print (2)
```

## In the Print Log Window

The Status column will show:

```
Execution Time        Template Name   Records  Printer     Status
--------------------- --------------- -------- ----------- -------------------------
2025-10-10 19:47:00   MyTemplate      0        Adobe PDF   No records to print (2)
2025-10-10 19:45:00   MyTemplate      10       Adobe PDF   Success
2025-10-10 19:44:00   MyTemplate      0        Adobe PDF   No records to print (5)
2025-10-10 19:30:00   MyTemplate      10       Adobe PDF   Success
```

You can see:
- The entry at 19:44:00 was checked 5 times before records appeared
- The entry at 19:47:00 has been checked 2 times so far

## Implementation Details

### Model Changes (`Models/ScheduledPrintLogEntry.cs`)

Added two new properties:

```csharp
/// <summary>
/// Count of how many times "No records" timestamp was updated (starts at 1)
/// </summary>
public int NoRecordsCheckCount { get; set; } = 1;

/// <summary>
/// Formatted status with check count for "No records" entries
/// </summary>
public string FormattedStatus => Status == "No records to print" && NoRecordsCheckCount > 1
    ? $"No records to print ({NoRecordsCheckCount})"
    : Status;
```

### Service Changes (`Services/ScheduledPrintingService.cs`)

Modified `AddNoRecordsLog()` to increment the count:

```csharp
if (lastLog.TemplateName == templateName && 
    lastLog.Status == "No records to print" && 
    lastLog.RecordCount == 0)
{
    // Update timestamp and increment check count
    lastLog.ExecutionTime = DateTime.Now;
    lastLog.NoRecordsCheckCount++;  // ← Increment count
    return;
}
```

When creating a new "No records" entry:

```csharp
var logEntry = new ScheduledPrintLogEntry
{
    ExecutionTime = DateTime.Now,
    TemplateName = templateName,
    RecordCount = 0,
    PrinterName = printerName,
    Status = "No records to print",
    UpdateErrors = string.Empty,
    NoRecordsCheckCount = 1  // ← Start at 1
};
```

### UI Changes (`UI/LogsWindow.xaml`)

Changed Status column binding:

```xaml
<!-- Before -->
<DataGridTextColumn Header="Status" Binding="{Binding Status}" Width="120"/>

<!-- After -->
<DataGridTextColumn Header="Status" Binding="{Binding FormattedStatus}" Width="200"/>
```

Note: Increased width from 120 to 200 to accommodate longer text with count.

## Benefits

1. **Activity Indicator**: Shows that scheduler is actively running (count increases)
2. **Wait Duration**: Indicates how long the system has been waiting for records
3. **Pattern Recognition**: Helps identify if certain times have no data consistently
4. **Troubleshooting**: Can determine if query is working (if count keeps rising, query may need adjustment)
5. **Visual Feedback**: User can see the scheduler is working without checking log files

## User Experience

### For Users:
- **Count not shown on first check**: Keeps display clean when only 1 check occurred
- **Count shown from 2nd check**: Indicates this is an ongoing "no records" state
- **Increases each minute**: Visual confirmation that scheduler is running
- **Resets on new session**: Each "no records" period is tracked separately

### For Troubleshooting:
- **High count (e.g., 50+)**: May indicate query needs adjustment or data pipeline issue
- **Alternating Success/No records**: Normal pattern for intermittent data
- **Steady increase**: System is waiting for new data to arrive

## Debug Logging

The debug log now includes the count:

```
[2025-10-10 19:41:00] No records found - skipping print job for MyTemplate
[2025-10-10 19:41:00] Updated timestamp for existing 'No records' log entry (count: 2)
```

```
[2025-10-10 19:42:00] No records found - skipping print job for MyTemplate
[2025-10-10 19:42:00] Updated timestamp for existing 'No records' log entry (count: 3)
```

This helps confirm the feature is working correctly during development/testing.

## Edge Cases Handled

1. **First check**: Shows `"No records to print"` without count
2. **Second check**: Shows `"No records to print (2)"`
3. **Different templates**: Each template has independent counter
4. **Records appear then disappear**: Counter resets for new "no records" session
5. **Window refresh**: Counter is persisted in the log entry object

## Related Files Modified
- `Models/ScheduledPrintLogEntry.cs` - Added `NoRecordsCheckCount` and `FormattedStatus` properties
- `Services/ScheduledPrintingService.cs` - Increments counter in `AddNoRecordsLog()`
- `UI/LogsWindow.xaml` - Changed binding to `FormattedStatus` and increased column width
