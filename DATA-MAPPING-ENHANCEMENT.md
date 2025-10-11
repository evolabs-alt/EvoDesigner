# Data Mapping Enhancement

## Overview
Added data field mapping dropdown to Barcode, QRCode, and DataMatrix elements in the Element Properties panel, ensuring consistency across all data-bindable element types.

## Changes Made

### File: `UI/MainWindow.xaml.cs`

#### 1. Enhanced Image Element Properties Handler
**Location:** Lines 2973-3079 (ShowProperties method)

**Previous Behavior:**
- All Image elements (regular images, barcodes, QR codes) showed only basic Width/Height properties
- No data field mapping available for barcodes/QR codes

**New Behavior:**
- Image handler now checks the Tag property to determine element type:
  - **QR Codes** (`QRCode` or `QRCodeData:*` tag):
    - Shows "QR Code Properties" header
    - Editable Data field (regenerates QR code on change)
    - Width and Height properties
    - **Data Field Mapping dropdown** ✅
  
  - **Barcodes** (`BarcodeData:*` tag, includes DataMatrix):
    - Shows "{Type} Barcode Properties" header
    - Read-only Symbology display (EAN13, EAN8, CODE128, DATAMATRIX, etc.)
    - Editable Data field (regenerates barcode on change)
    - Width and Height properties
    - **Data Field Mapping dropdown** ✅
  
  - **Regular Images**:
    - Shows "Image Properties" header
    - Width and Height properties
    - No data mapping (as intended)

#### 2. Added Helper Method
**Method:** `AddImageSizeProperties(Image img)`
**Location:** Lines 3131-3163

**Purpose:**
- Extracted common Width/Height property logic
- Reduces code duplication
- Used by QR Code, Barcode, and regular Image handlers

## Element Type Summary

### Elements WITH Data Field Mapping:
✅ **Text** - Border with TextBlock child
✅ **Barcode (EAN-13)** - Image with `BarcodeData:EAN13:*` tag
✅ **Barcode (EAN-8)** - Image with `BarcodeData:EAN8:*` tag
✅ **Barcode (CODE-128)** - Image with `BarcodeData:CODE128:*` tag
✅ **Barcode (CODE-39)** - Image with `BarcodeData:CODE39:*` tag
✅ **Barcode (UPC-A)** - Image with `BarcodeData:UPCA:*` tag
✅ **Barcode (UPC-E)** - Image with `BarcodeData:UPCE:*` tag
✅ **QR Code** - Image with `QRCode` or `QRCodeData:*` tag
✅ **DataMatrix** - Image with `BarcodeData:DATAMATRIX:*` tag
✅ **Aztec** - Image with `BarcodeData:AZTEC:*` tag
✅ **PDF417** - Image with `BarcodeData:PDF417:*` tag

### Elements WITHOUT Data Field Mapping (as intended):
❌ **Rectangle** - Decorative shape
❌ **Ellipse** - Decorative shape
❌ **Line** - Decorative shape
❌ **Image** - Static image file
❌ **LinkedImage** - URL-based image (could be enhanced later if needed)

## Technical Details

### Tag Format Recognition
```csharp
// QR Code detection
if (tagStr == "QRCode" || tagStr.StartsWith("QRCodeData:"))

// Barcode detection (includes all barcode types)
if (tagStr.StartsWith("BarcodeData:"))
    // Format: BarcodeData:{SYMBOLOGY}:{DATA}
    // Example: BarcodeData:EAN13:1234567890123
```

### Barcode Regeneration
When the user edits barcode/QR code data in the properties panel:
1. Tag is updated with new data
2. `CreateBarcodeImage()` method is called with current dimensions
3. Image source is replaced with newly generated barcode/QR code
4. Template is marked as dirty for save prompt

### Data Field Mapping Integration
The data field mapping dropdown:
- Only appears when a query has been executed (lastResultTable is populated)
- Shows all column names from the last query result
- Stores mapping in `CurrentTemplate.FieldMappings` collection
- Maps element index to field name for data replacement during printing

## User Experience

### Before
1. User adds a QR Code to the label
2. Clicks on it to view properties
3. Sees only Width/Height - **no way to bind to database field**
4. Must manually type `{field_name}` placeholder in the data field

### After
1. User adds a QR Code to the label
2. Clicks on it to view properties
3. Sees:
   - QR Code Properties header
   - Data field (editable)
   - Width and Height
   - **Data Field Mapping dropdown with all available columns** ✅
4. User selects a column from dropdown
5. Element automatically binds to that database field

## Testing Recommendations

1. **Add each barcode type** and verify data mapping dropdown appears:
   - EAN-13
   - EAN-8
   - CODE-128
   - DataMatrix
   
2. **Add QR Code** and verify data mapping dropdown appears

3. **Add regular Image** and verify NO data mapping dropdown (correct behavior)

4. **Test data binding workflow:**
   - Configure data source with query
   - Click "Run Now" to populate lastResultTable
   - Add QR Code element
   - Select it and choose a column from data mapping dropdown
   - Save template
   - Print/preview to verify data replacement works

5. **Test barcode data editing:**
   - Select barcode element
   - Change data in properties panel
   - Verify barcode image regenerates immediately
   - Verify symbology is displayed correctly and read-only

## Build Status
✅ Build succeeded with 0 errors
⚠️ 67 warnings (nullable reference warnings - existing codebase pattern)

## Date
October 10, 2025
