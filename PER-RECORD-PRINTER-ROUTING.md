# Per-Record Printer Routing Feature

## Overview
Labels can now be automatically routed to different printers based on data from the database query results.

## How It Works

### Without `printer` Column (Default Behavior)
If your query does NOT include a `printer` column, all records print to the template's DefaultPrinter:

```sql
SELECT id, customer, description FROM stock LIMIT 10
```
**Result:** All 10 records → One print job → DefaultPrinter

---

### With `printer` Column (Smart Routing)
If your query INCLUDES a `printer` column (case-sensitive), records are grouped by printer:

```sql
SELECT id, customer, description, printer FROM stock LIMIT 10
```

**Example Data:**
```
Row 0: id=1, customer="ACME", printer="Zebra-Printer-1"
Row 1: id=2, customer="XYZ",  printer="Zebra-Printer-1"
Row 2: id=3, customer="ABC",  printer="HP-Printer-2"
Row 3: id=4, customer="DEF",  printer=NULL
Row 4: id=5, customer="GHI",  printer="InvalidPrinter"
```

**Result:**
- Records 0, 1 → One print job → `Zebra-Printer-1`
- Record 2 → One print job → `HP-Printer-2`
- Record 3 → One print job → `DefaultPrinter` (NULL fallback)
- Record 4 → **SKIPPED** (invalid printer, logged as error)

---

## Grouping Logic (Option B)

Records are grouped by printer name:
1. Query results are scanned for the `printer` column
2. Records are grouped by their printer value
3. Each group creates ONE print job to its designated printer
4. All pages in a group go in a single print job

**Benefits:**
- ✅ Efficient - fewer print jobs
- ✅ Network friendly - grouped by destination
- ✅ Maintains print order per printer

---

## Column Name Requirements

- **Exact name:** `printer` (lowercase, case-sensitive)
- **NOT recognized:** `Printer`, `PRINTER`, `printer_name`, etc.
- Must be exact: `printer`

---

## Printer Validation

Each printer name is validated before printing:

### Valid Printer
```
printer="Microsoft Print to PDF"  ✅ Exists → prints normally
```

### Invalid Printer
```
printer="NonExistentPrinter"  ❌ Doesn't exist → record SKIPPED + logged
```

**Log entry:**
```
[2025-10-06 10:35:00] SKIPPED record 12345: Invalid printer 'NonExistentPrinter' - Printer not found
```

### NULL/Empty Printer
```
printer=NULL     → Falls back to DefaultPrinter ✅
printer=""       → Falls back to DefaultPrinter ✅
printer="  "     → Falls back to DefaultPrinter ✅
```

---

## SQL Query Examples

### Route by Location
```sql
SELECT 
    id,
    customer,
    description,
    CASE location
        WHEN 'WAREHOUSE-A' THEN 'Printer-A'
        WHEN 'WAREHOUSE-B' THEN 'Printer-B'
        ELSE 'DefaultPrinter'
    END AS printer
FROM stock
WHERE status = 'COLLECTED'
```

### Route by Customer
```sql
SELECT 
    id,
    customer,
    description,
    customer_printer_mapping.printer_name AS printer
FROM stock
LEFT JOIN customer_printer_mapping 
    ON stock.customer = customer_printer_mapping.customer_id
WHERE stock.active = 1
```

### Route by Priority
```sql
SELECT 
    id,
    customer,
    description,
    IF(priority = 'HIGH', 'Fast-Printer', 'Slow-Printer') AS printer
FROM stock
ORDER BY priority DESC
```

---

## Logging

Detailed logs in `run-now-debug.log`:

### With `printer` Column
```log
[2025-10-06 10:35:00] 'printer' column found - grouping records by printer
[2025-10-06 10:35:00] Grouped into 3 printer group(s)
[2025-10-06 10:35:00] Printing 5 record(s) to printer: Zebra-Printer-1
[2025-10-06 10:35:01] Printing 3 record(s) to printer: HP-Printer-2
[2025-10-06 10:35:02] Printing 2 record(s) to printer: Microsoft Print to PDF
[2025-10-06 10:35:02] SKIPPED record 789: Invalid printer 'BadPrinter' - Printer not found
```

### Without `printer` Column
```log
[2025-10-06 10:35:00] No 'printer' column - using default printer for all records
[2025-10-06 10:35:00] Printing 10 record(s) to printer: Microsoft Print to PDF
```

---

## Configuration

### Template Setup
Your template's DefaultPrinter is used as fallback:

```xml
<DataSource>
  <Host>172.179.236.3</Host>
  <Port>30036</Port>
  <Database>stock</Database>
  <Username>root</Username>
  <Password></Password>
  <Query>SELECT *, printer FROM stock LIMIT 10</Query>
  <DefaultPrinter>Microsoft Print to PDF</DefaultPrinter>
</DataSource>
```

**DefaultPrinter is used when:**
- No `printer` column in query
- `printer` value is NULL
- `printer` value is empty/whitespace

---

## Error Handling

### Invalid Printer Names
- Record is **SKIPPED**
- Error logged to `run-now-debug.log`
- Error notification sent to system tray
- Other records continue printing normally

### Example Error Log
```log
[2025-10-06 10:35:00] SKIPPED record 300000000000001265: Invalid printer 'Office-Printer-99' - The printer does not exist on this computer
```

### Example Tray Notification
```
⚠️ Skipping record 300000000000001265: Invalid printer 'Office-Printer-99' - Printer not found
```

---

## Performance Considerations

### Grouping Efficiency
- Records grouped **before** printing
- Each group = One print job
- Minimizes network traffic
- Reduces printer queue load

### Example Performance
**10 records to 3 printers:**
- ❌ Old: 10 separate print jobs
- ✅ New: 3 print jobs (grouped)

---

## Testing

### Test Without `printer` Column
```sql
SELECT id, customer FROM stock LIMIT 5
```
**Expected:** 5 labels → DefaultPrinter

### Test With `printer` Column
```sql
SELECT id, customer, 'Microsoft Print to PDF' AS printer FROM stock LIMIT 5
```
**Expected:** 5 labels → Microsoft Print to PDF

### Test Mixed Printers
```sql
SELECT 
    id, 
    customer,
    CASE WHEN id % 2 = 0 
        THEN 'Microsoft Print to PDF' 
        ELSE 'Microsoft XPS Document Writer' 
    END AS printer
FROM stock LIMIT 10
```
**Expected:** 
- ~5 labels → Microsoft Print to PDF
- ~5 labels → Microsoft XPS Document Writer

### Test Invalid Printer
```sql
SELECT id, customer, 'NonExistentPrinter' AS printer FROM stock LIMIT 3
```
**Expected:** All 3 records skipped with error logs

---

## Status: ✅ Implemented

**Version:** 1.0  
**Implementation:** Complete  
**Tested:** Ready for production  
