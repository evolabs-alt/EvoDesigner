# Printer Fallback Display Enhancement

## Overview
Enhanced the print log to show which original printer names were requested when they weren't found and the system used a fallback printer.

## User Request
"If the printer was not found and was defaulted, then in the print log under print lets put in brackets ({original printer} not found)"

## Implementation

### File: `Services/ScheduledPrintingService.cs`

#### Changes Made

1. **Added Fallback Tracking Dictionary** (Line 358)
   ```csharp
   // Track printer fallbacks: actual printer -> original requested printer
   var printerFallbacks = new Dictionary<string, HashSet<string>>();
   ```

2. **Track Original Printer Name** (Line 368)
   ```csharp
   string originalPrinter = targetPrinter; // Save original for fallback tracking
   ```

3. **Record Fallback When Printer Changes** (Lines 388-394)
   ```csharp
   // Track fallback if printer changed
   if (targetPrinter != originalPrinter)
   {
       if (!printerFallbacks.ContainsKey(targetPrinter))
       {
           printerFallbacks[targetPrinter] = new HashSet<string>();
       }
       printerFallbacks[targetPrinter].Add(originalPrinter);
   }
   ```

4. **Display Fallback Info in Log** (Lines 415-421)
   ```csharp
   // Build printer name with fallback info if applicable
   string printerDisplayName = groupPrinter;
   if (printerFallbacks.ContainsKey(groupPrinter))
   {
       var originalPrinters = string.Join(", ", printerFallbacks[groupPrinter]);
       printerDisplayName = $"{groupPrinter} ({originalPrinters} not found)";
   }
   
   // Log each printer group execution
   AddLog(templateName, groupData.Rows.Count, printerDisplayName);
   ```

## How It Works

### Scenario 1: Single Printer Not Found
**Data:**
- Record 1: printer = "WC_MP 1" (not found)
- Record 2: printer = "WC_MP 1" (not found)
- Record 3: printer = "WC_MP 1" (not found)

**Result in Print Log:**
```
Printer: Adobe PDF (WC_MP 1 not found)
Records: 3
Status: Success
```

### Scenario 2: Multiple Different Printers Not Found (Same Fallback)
**Data:**
- Record 1: printer = "WC_MP 1" (not found → Adobe PDF)
- Record 2: printer = "WC_MP 2" (not found → Adobe PDF)
- Record 3: printer = "HP LaserJet 5" (not found → Adobe PDF)

**Result in Print Log:**
```
Printer: Adobe PDF (WC_MP 1, WC_MP 2, HP LaserJet 5 not found)
Records: 3
Status: Success
```

### Scenario 3: Mixed Valid and Invalid Printers
**Data:**
- Record 1: printer = "Adobe PDF" (valid)
- Record 2: printer = "WC_MP 1" (not found → Adobe PDF)
- Record 3: printer = "HP LaserJet 4000" (valid)
- Record 4: printer = "WC_MP 2" (not found → Adobe PDF)

**Result in Print Log:**
```
Printer: Adobe PDF (WC_MP 1, WC_MP 2 not found)
Records: 3

Printer: HP LaserJet 4000
Records: 1
```

### Scenario 4: All Printers Valid (No Fallback)
**Data:**
- Record 1: printer = "Adobe PDF" (valid)
- Record 2: printer = "HP LaserJet 4000" (valid)

**Result in Print Log:**
```
Printer: Adobe PDF
Records: 1

Printer: HP LaserJet 4000
Records: 1
```

## Technical Details

### Data Structure
- **Dictionary<string, HashSet<string>>**: Maps actual printer used → set of original printer names requested
- **HashSet** prevents duplicate original printer names in the display
- **string.Join(", ", ...)** creates comma-separated list for multiple fallbacks

### Why HashSet?
If 10 records all request "WC_MP 1" and it falls back to "Adobe PDF", the HashSet ensures we only show:
```
Adobe PDF (WC_MP 1 not found)
```
Instead of:
```
Adobe PDF (WC_MP 1, WC_MP 1, WC_MP 1, WC_MP 1, ... not found)
```

## User Experience

### Before
```
Printer: Adobe PDF
Records: 5
Status: Success
```
*User has no idea that these were supposed to go to "WC_MP 1"*

### After
```
Printer: Adobe PDF (WC_MP 1 not found)
Records: 5
Status: Success
```
*User immediately understands the fallback occurred*

## Log File Enhancement
The debug log (`run-now-debug.log`) already shows individual record warnings:
```
[2025-10-10 21:01:08] Record 2291: Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'
[2025-10-10 21:01:08] Record 2292: Preferred printer 'WC_MP 1' not found, using system default 'Adobe PDF'
```

The print log now provides a **summary view** of these fallbacks in the UI, making it easy to see at a glance which printers had issues without scrolling through the detailed debug log.

## Build Status
✅ Build succeeded with 0 errors

## Date
October 10, 2025
