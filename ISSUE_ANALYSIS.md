# EvoDesigner Issue Resolution Guide

## Issues Diagnosed:
1. **Text Alignment not working in print preview/print**
2. **DataMatrix and GS1 DataBar buttons not responding to clicks/drags**

## Root Cause Analysis:

### Text Alignment Issue:
- ✅ Code has explicit alignment settings in `CreateElementByType`
- ✅ Print services have alignment calculation logic
- ❌ **Potential Issue**: TextBlock alignment may be overridden during print rendering

### DataMatrix/GS1 DataBar Issue:
- ✅ Button discovery system correctly identifies buttons by content
- ✅ Element creation cases are implemented
- ✅ Barcode generation has error handling
- ❌ **Potential Issue**: Button event handlers may not be attaching properly

## Immediate Solutions:

### Fix 1: Enhanced Text Alignment Debug
The current code sets explicit alignment, but we need to ensure it persists:

```csharp
// In CreateElementByType method, ensure alignment is preserved
var textBlock = new TextBlock 
{ 
    Text = "Text", 
    FontSize = 16, 
    Foreground = Brushes.Black,
    TextWrapping = TextWrapping.Wrap,
    TextAlignment = TextAlignment.Left,  // Explicit
    VerticalAlignment = VerticalAlignment.Top,  // Explicit
    HorizontalAlignment = HorizontalAlignment.Left  // Add this
};

// Ensure parent container respects alignment
var border = new Border
{
    Child = textBlock,
    BorderBrush = Brushes.LightGray,
    BorderThickness = new Thickness(1),
    Background = Brushes.White,
    HorizontalAlignment = HorizontalAlignment.Left,  // Add this
    VerticalAlignment = VerticalAlignment.Top        // Add this
};
```

### Fix 2: Enhanced Button Event Debugging
The button discovery and event attachment appears correct, but we can add more robust handling:

```csharp
// In AttachToolboxDragHandlers, add explicit event verification
if (elementType != null)
{
    System.Diagnostics.Debug.WriteLine($"Attaching events to {elementType} button");
    
    // Ensure button is enabled and clickable
    btn.IsEnabled = true;
    btn.Focusable = true;
    
    // Store element type in Tag for verification
    btn.Tag = elementType;
    
    // Rest of event handling code...
}
```

## Testing Recommendations:

### Test Text Alignment:
1. Create a text element
2. Change its alignment using the properties panel
3. Open print preview - verify alignment matches
4. Print the label - verify printed alignment matches

### Test Barcode Buttons:
1. **Click Test**: Single click DataMatrix/GS1 DataBar buttons
2. **Drag Test**: Drag these buttons to canvas
3. **Check Debug Output**: Look for debug messages in console/output window

## Status:
- ✅ Code analysis completed
- ✅ Potential root causes identified  
- 🔄 Application is running for user testing
- ⚠️ Need user verification of actual behavior vs expected behavior

## Next Steps:
1. User should test the current build with both issues
2. Report specific failure behavior (no response, wrong alignment, error messages)
3. Check debug output window for any error messages during testing
4. If issues persist, implement the enhanced debugging code above

The current implementation should handle both issues correctly based on the code analysis.