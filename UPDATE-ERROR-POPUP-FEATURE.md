# Update Error Popup Feature

## Overview
Added functionality to display UPDATE query errors in a popup when clicking on log records in the Print Log window.

## Changes Made

### 1. Model Updates (`Models/ScheduledPrintLogEntry.cs`)
- Added `UpdateErrors` property to store error messages from failed UPDATE queries
- Errors are now captured and stored with each log entry

### 2. Service Updates (`Services/ScheduledPrintingService.cs`)
- Modified `MarkRecordsAsPrinted()` to return error details as a string
- Captures the first error message from failed UPDATE queries
- Returns a summary like: "10 update(s) failed. Example error:\nRecord 300000000000001267: Cannot add or update..."
- Modified `AddLog()` to accept an optional `updateErrors` parameter
- Updated `SendPrintJob()` to capture update errors and pass them to the log entry
- Log status changes to "Success (Update errors)" when updates fail but print succeeds

### 3. UI Updates (`UI/LogsWindow.xaml` and `LogsWindow.xaml.cs`)
- Added `MouseDoubleClick` event handler to the DataGrid
- Double-clicking a log record now shows a popup with error details
- If no errors exist, shows "No update errors recorded for this print job"
- If errors exist, displays them in a warning MessageBox

## Usage

### For Users:
1. Run the application with scheduled printing enabled
2. Open the Print Logs window (View → Scheduled Print Logs)
3. **Double-click** any log entry to see UPDATE error details
4. If the UPDATE query failed, you'll see:
   - Number of failed updates
   - Example error message with record ID
   - Full SQL error details (e.g., foreign key constraint violations)

### Example Error Message:
```
10 update(s) failed. Example error:
Record 300000000000001267: Cannot add or update a child row: a foreign key constraint fails (`stock`.`stock`, CONSTRAINT `FKha0pnj95eo1w36yuj2i2rfl1e` FOREIGN KEY (`pack`) REFERENCES `pack` (`id`))
```

## Troubleshooting UPDATE Query Errors

### Common Issue: Foreign Key Constraint Violations
**Error:** `Cannot add or update a child row: a foreign key constraint fails`

**Cause:** Your UPDATE query is trying to modify a column that has a foreign key constraint to another table.

**Solution Options:**

#### Option 1: Use a Different Column
Instead of updating the `pack` column, use another column to mark records as printed:
```sql
UPDATE stock SET remarks = 'PRINTED' WHERE id = {id}
```

#### Option 2: Add a Dedicated Column
Add a new column specifically for tracking printed status:
```sql
ALTER TABLE stock ADD COLUMN printed BOOLEAN DEFAULT 0;
```

Then use:
```sql
UPDATE stock SET printed = 1 WHERE id = {id}
```

#### Option 3: Use Valid Foreign Key Value
If you must update the `pack` column, ensure the value exists in the `pack` table:
```sql
-- First, find a valid pack ID
SELECT id FROM pack WHERE id = 9999;

-- If it doesn't exist, create it
INSERT INTO pack (id, name) VALUES (9999, 'PRINTED');

-- Then your UPDATE will work
UPDATE stock SET pack = 9999 WHERE id = {id}
```

## Debug Logging
All UPDATE queries are logged to `run-now-debug.log` with:
- The actual SQL being executed (with placeholders replaced)
- Success/failure status for each record
- Full error messages for debugging

Example log output:
```
[2025-10-10 19:35:53] Executing update query for 10 records
[2025-10-10 19:35:53] Executing SQL: UPDATE stock SET pack = 9999 WHERE id = 300000000000001267
[2025-10-10 19:35:54] ERROR marking record 300000000000001267: Cannot add or update a child row...
```

## Technical Details

### Error Capture Logic
```csharp
// In MarkRecordsAsPrinted():
var errorMessages = new StringBuilder();

foreach (DataRow row in dataTable.Rows)
{
    try
    {
        // Execute UPDATE
        await Task.Run(() => mysqlService.ExecuteNonQuery(query));
        successCount++;
    }
    catch (Exception ex)
    {
        failureCount++;
        var recordId = row.ItemArray.Length > 0 ? row.ItemArray[0]?.ToString() : "unknown";
        var errorMsg = $"Record {recordId}: {ex.Message}";
        
        // Store only the first error to avoid excessive text
        if (errorMessages.Length == 0)
        {
            errorMessages.AppendLine(errorMsg);
        }
    }
}

return failureCount > 0 
    ? $"{failureCount} update(s) failed. Example error:\n{errorMessages}" 
    : string.Empty;
```

### Status Display
- **"Success"** - Print succeeded, no UPDATE query configured or all updates succeeded
- **"Success (Update errors)"** - Print succeeded but UPDATE queries failed
- **"Error: [message]"** - Print job itself failed

## Benefits
1. **Immediate Error Visibility** - Users can see why records weren't marked as printed
2. **Debugging Made Easy** - Actual SQL and error messages help diagnose issues
3. **Non-Intrusive** - Errors don't block printing, just logged for review
4. **User-Friendly** - Simple double-click to view details instead of reading log files
