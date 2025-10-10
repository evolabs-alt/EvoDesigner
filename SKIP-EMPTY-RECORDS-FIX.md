# Skip Printing When No Records Found - Fix

## Issue
When the scheduler runs and the database query returns no records (0 rows), it was still printing one blank label with static content. This wastes paper and is not the expected behavior in FLOW mode.

## Root Cause
In `Services/ScheduledPrintingService.cs`, the `SendPrintJob()` method had an `else` clause that would print one label even when there were no data records:

```csharp
if (dataTable != null && dataTable.Rows.Count > 0)
{
    // Print records
}
else
{
    // No data - print one label with static content
    printService.Print(template, printerName, dataTable: null, recordIndex: 0, copies: 1);
}
```

This made sense for templates without data binding (static labels), but in FLOW mode where templates are specifically designed to print database records, printing when there are no records is incorrect.

## Solution
Added an early return check at the beginning of `SendPrintJob()` to skip printing entirely when there are no records:

```csharp
// Check if there are records to print
if (dataTable == null || dataTable.Rows.Count == 0)
{
    LogMessage?.Invoke(this, $"No records to print for {templateName}. Skipping print job.");
    File.AppendAllText("run-now-debug.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] No records found - skipping print job for {templateName}" + Environment.NewLine);
    return; // Exit early - don't print anything
}
```

This ensures:
1. **No wasteful printing** - If query returns 0 records, nothing gets printed
2. **Clear logging** - Log message indicates "No records to print" and "skipping print job"
3. **Proper flow** - Next scheduler iteration will check again for new records

## Behavior Changes

### Before Fix:
- Query returns 0 records → Prints 1 blank label
- Wastes paper on every scheduler run until records appear
- Confusing for users (why is it printing blanks?)

### After Fix:
- Query returns 0 records → **No printing occurs**
- Log message: "No records to print for [template]. Skipping print job."
- Scheduler continues to check on next interval
- When records appear, printing resumes normally

## Example Log Output

### When records are found:
```
[2025-10-10 19:40:00] Executing query: SELECT * FROM stock WHERE pack != 9999 LIMIT 10
[2025-10-10 19:40:00] Query returned 10 records
[2025-10-10 19:40:00] Printing 10 label(s) for MyTemplate
[2025-10-10 19:40:00] Sending print job...
```

### When no records are found:
```
[2025-10-10 19:45:00] Executing query: SELECT * FROM stock WHERE pack != 9999 LIMIT 10
[2025-10-10 19:45:00] Query returned 0 records
[2025-10-10 19:45:00] No records found - skipping print job for MyTemplate
```

## Impact on Different Scenarios

### ✅ FLOW Mode (Data-Bound Templates)
- **Intended behavior**: Only print when there are records to print
- **Result**: ✅ Working correctly - no blank labels printed

### ✅ Static Templates (No Data Binding)
- **Intended behavior**: N/A - FLOW mode is for data-bound templates only
- **Note**: Templates without data sources won't be processed by the scheduler
- **Manual printing**: Still works normally for static templates via main window

## Testing Recommendations

1. **Test with empty result set:**
   - Set query to return 0 records (e.g., `WHERE 1=0`)
   - Run scheduler
   - Verify: No print job sent, log shows "No records found - skipping"

2. **Test with records appearing:**
   - Start with empty result set (0 records)
   - Verify: No printing occurs
   - Add records to database
   - Wait for next scheduler interval
   - Verify: Labels print normally

3. **Test with alternating results:**
   - Records appear and disappear based on query conditions
   - Verify: Only prints when records exist

## Related Files Modified
- `Services/ScheduledPrintingService.cs` - Added early return check in `SendPrintJob()`

## Benefits
1. **Cost Savings** - No wasted paper/labels on empty queries
2. **Cleaner Operation** - Scheduler only acts when there's work to do
3. **Better Logging** - Clear indication when no records are found
4. **Expected Behavior** - Matches user expectations for FLOW mode
