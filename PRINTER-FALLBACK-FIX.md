# Printer Fallback Bug Fix

## Issue Description

**Problem:** Records were being **SKIPPED** when their preferred printer wasn't found, even though the system had a fallback mechanism to use the default printer (Adobe PDF).

**Log Evidence:**
```
[2025-10-10 21:01:08] SKIPPED record 2291: Invalid printer 'WC_MP 1' - Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'
```

The message said "using system default 'Adobe PDF'" but nothing was actually printed to Adobe PDF because the record was skipped.

## Root Cause

In `ScheduledPrintingService.cs` line 370, the code was checking:

```csharp
if (!string.IsNullOrWhiteSpace(printerValidation.Warning))
{
    // Skip this record
    continue;
}
```

This incorrectly treated **any warning** as a reason to skip the record, even when the printer validation was successful with a fallback.

### How PrinterService.ValidatePrinter() Works

```csharp
public class PrinterValidationResult
{
    public bool IsValid { get; set; }       // TRUE if can print (even with fallback)
    public string SelectedPrinter { get; set; }  // The printer to use
    public string Warning { get; set; }     // Optional warning message
}
```

**Scenarios:**

1. **Printer exists** → `IsValid=true`, `Warning=""`, `SelectedPrinter="WC_MP 1"`
2. **Printer not found (fallback)** → `IsValid=true`, `Warning="Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'"`, `SelectedPrinter="Adobe PDF"`
3. **No printers on system** → `IsValid=false`, `Warning="No printers are installed on the system"`

## Fix Applied

**File:** `Services/ScheduledPrintingService.cs`
**Location:** Lines 368-381

### Before (Incorrect):
```csharp
var printerValidation = PrinterService.ValidatePrinter(targetPrinter);
if (!string.IsNullOrWhiteSpace(printerValidation.Warning))
{
    // WRONG: Skips record even when IsValid=true with fallback
    ErrorOccurred?.Invoke(this, $"Skipping record {recordId}: Invalid printer '{targetPrinter}' - {printerValidation.Warning}");
    File.AppendAllText("run-now-debug.log", $"[...] SKIPPED record {recordId}: ...");
    continue;  // Record is skipped!
}
targetPrinter = printerValidation.SelectedPrinter;
```

### After (Correct):
```csharp
var printerValidation = PrinterService.ValidatePrinter(targetPrinter);
if (!printerValidation.IsValid)
{
    // CORRECT: Only skip when validation actually failed
    var recordId = row.ItemArray.Length > 0 ? row.ItemArray[0]?.ToString() : "unknown";
    ErrorOccurred?.Invoke(this, $"Skipping record {recordId}: {printerValidation.Warning}");
    File.AppendAllText("run-now-debug.log", $"[...] SKIPPED record {recordId}: ...");
    continue;
}

// Log warning if printer fallback occurred (but continue printing)
if (!string.IsNullOrWhiteSpace(printerValidation.Warning))
{
    var recordId = row.ItemArray.Length > 0 ? row.ItemArray[0]?.ToString() : "unknown";
    File.AppendAllText("run-now-debug.log", $"[...] Record {recordId}: {printerValidation.Warning}");
}

targetPrinter = printerValidation.SelectedPrinter;
```

## Expected Behavior After Fix

### Scenario 1: Preferred Printer Not Found (Fallback Available)
**Before:**
```
[2025-10-10 21:01:08] SKIPPED record 2291: Invalid printer 'WC_MP 1' - Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'
```
→ Record 2291 was NOT printed at all

**After:**
```
[2025-10-10 21:01:08] Record 2291: Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'
[2025-10-10 21:01:08] Grouped into 1 printer group(s)
[2025-10-10 21:01:08] Printing 1 record(s) to printer 'Adobe PDF'
```
→ Record 2291 IS printed to Adobe PDF

### Scenario 2: No Printers on System (True Failure)
```
[2025-10-10 21:01:08] SKIPPED record 2291: No printers are installed on the system
```
→ Record 2291 is correctly skipped (no fallback possible)

## Impact

### Fixed Issues:
✅ Records with invalid printer names are now printed to the default printer instead of being skipped
✅ Warning messages are logged but don't prevent printing
✅ Only true printer failures (no printers on system) cause records to be skipped

### Unchanged Behavior:
✅ Records with valid printer names continue to print normally
✅ Default printer fallback mechanism works as originally intended
✅ Error logging and user notifications still occur

## Testing Recommendations

1. **Test printer fallback scenario:**
   - Configure a record with `printer = 'NonExistentPrinter'`
   - Run scheduled print
   - Verify record prints to default printer (Adobe PDF)
   - Check log shows warning but NOT "SKIPPED"

2. **Test valid printer scenario:**
   - Configure a record with valid printer name
   - Run scheduled print
   - Verify record prints to specified printer
   - Check log shows no warnings

3. **Test no printers scenario (if possible):**
   - Disable all printers temporarily
   - Run scheduled print
   - Verify records are skipped with appropriate error message

## Build Status
✅ Build succeeded with 0 errors

## Date
October 10, 2025
