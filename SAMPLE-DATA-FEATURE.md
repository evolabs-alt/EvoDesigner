# Sample Data Preview Feature

## Overview
Added a new "Sample Data" menu item to the Data menu that opens a window displaying up to 10 sample records from the configured data source. This allows users to preview their data without printing or running the full template.

## Location
**Menu Path:** `Data → Sample Data...`

## Features

### 1. Sample Data Window
- **Title:** "Sample Data Preview"
- **Icon:** 📊
- **Size:** 1000×600 pixels (resizable)
- **Position:** Center of main window

### 2. Data Display
- Shows up to 10 records from the data source
- Auto-generates columns based on query results
- Modern DataGrid with:
  - Blue header (#0078d4)
  - Alternating row colors for better readability
  - Grid lines for clear cell separation
  - Horizontal and vertical scrolling

### 3. Query Handling
- Automatically adds `LIMIT 10` to queries without a limit
- Preserves existing LIMIT if query already has one
- Uses the exact same query configured in Data Source setup

### 4. Status Information
- **Header Subtitle:** Shows database and host information
- **Footer Status:** Displays row and column counts
- Example: `"Showing 10 records with 35 columns"`

### 5. Error Handling
- No data source configured → Shows helpful message
- Query returns no records → Clear notification
- Database connection errors → Error dialog with details

## User Interface

### Window Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Sample Data Preview                           [↻ Refresh]   │
│ Showing up to 10 sample records from the data source        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ id  │ name     │ description │ price │ ... │ status  │  │
│  ├──────────────────────────────────────────────────────┤  │
│  │ 1   │ Product A│ Sample desc │ 9.99  │ ... │ Active  │  │
│  │ 2   │ Product B│ Another one │ 14.99 │ ... │ Active  │  │
│  │ ... │ ...      │ ...         │ ...   │ ... │ ...     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
├─────────────────────────────────────────────────────────────┤
│ Showing 10 records with 35 columns              [Close]     │
└─────────────────────────────────────────────────────────────┘
```

### Header
- **Large Title:** "Sample Data Preview"
- **Subtitle:** Database connection info (e.g., "Sample data from: mydb @ localhost")
- **Refresh Button:** Reload data from database

### Data Grid
- **Auto-generated columns** from query results
- **Blue header row** with white text
- **Alternating row colors** (white/#faf9f8)
- **Scrollable** both horizontally and vertically
- **Read-only** (no editing)

### Footer
- **Status text:** Record and column counts
- **Close button:** Dismiss the window

## Usage Examples

### Scenario 1: Preview Data Before Printing
1. Configure data source via `Data → Setup Data Source`
2. Click `Data → Sample Data`
3. Review the 10 sample records
4. Verify column names and data format
5. Close window and proceed with label design

### Scenario 2: Verify Database Connection
1. Click `Data → Sample Data`
2. If connection works: See sample records
3. If connection fails: See error message with details
4. Fix connection settings if needed
5. Click Refresh to retry

### Scenario 3: Check Query Results
1. Modify query in Data Source window
2. Click `Data → Sample Data`
3. Verify query returns expected data
4. Click Refresh to test query changes
5. Adjust query as needed

## Technical Implementation

### Files Created

#### 1. `UI/SampleDataWindow.xaml`
Modern WPF window with:
- DataGrid for displaying records
- Header with title and refresh button
- Footer with status and close button
- Professional styling matching application theme

#### 2. `UI/SampleDataWindow.xaml.cs`
Code-behind with:
- Constructor accepting `LabelTemplate` parameter
- `LoadSampleData()` method to fetch and display data
- Query modification to add `LIMIT 10`
- Error handling for connection issues
- Status text updates

### Files Modified

#### 3. `UI/MainWindow.xaml`
Added menu item:
```xaml
<MenuItem Header="_Sample Data..." 
          x:Name="SampleDataMenuItem" 
          Click="SampleData_Click">
    <MenuItem.Icon>
        <TextBlock Text="📊" FontSize="14" />
    </MenuItem.Icon>
</MenuItem>
```

#### 4. `UI/MainWindow.xaml.cs`
Added click handler:
```csharp
private void SampleData_Click(object sender, RoutedEventArgs e)
{
    // Check if data source is configured
    if (CurrentTemplate?.DataSource == null || 
        string.IsNullOrWhiteSpace(CurrentTemplate.DataSource.Query))
    {
        MessageBox.Show(...);
        return;
    }

    // Open sample data window
    var sampleDataWindow = new UI.SampleDataWindow(CurrentTemplate);
    sampleDataWindow.Owner = this;
    sampleDataWindow.ShowDialog();
}
```

## Data Source Integration

### Query Modification Logic
```csharp
string sampleQuery = _template.DataSource.Query.Trim();

// Check if query already has a LIMIT clause
if (sampleQuery.ToUpper().Contains("LIMIT"))
{
    // Use as-is (user may have intentionally limited it)
}
else
{
    // Add LIMIT 10
    sampleQuery += " LIMIT 10";
}
```

### Connection String
Uses the same connection string as the main template:
```csharp
var connectionString = $"Server={_template.DataSource.Host};" +
                      $"Port={_template.DataSource.Port};" +
                      $"Database={_template.DataSource.Database};" +
                      $"Uid={_template.DataSource.Username};" +
                      $"Pwd={_template.DataSource.Password};";
```

## Status Messages

### Success States
- `"Showing 10 records with 35 columns"` - Data loaded successfully
- `"Showing 5 records with 12 columns"` - Fewer than 10 records available

### Warning States
- `"No data source configured. Please setup a data source first."` - No data source
- `"Query returned no records"` - Query executed but returned 0 rows

### Error States
- `"Error loading data"` - Database connection or query error
- Shows detailed error message in footer subtitle

## Benefits

1. **Quick Preview:** See actual data without printing
2. **Verify Queries:** Test queries before using in templates
3. **Column Discovery:** See available field names for mapping
4. **Connection Testing:** Verify database connectivity
5. **Data Validation:** Ensure data format is correct
6. **No Side Effects:** Read-only, doesn't modify data

## User Workflow Integration

### Before This Feature:
1. Setup data source
2. Design label
3. Print preview (prints all records)
4. Hope data is correct

### With This Feature:
1. Setup data source
2. **Preview sample data** ← New step!
3. Verify data looks correct
4. Design label with confidence
5. Print preview
6. Print with accurate expectations

## Keyboard Shortcuts
- **Access key:** `Alt+D` then `S` (Data → Sample Data)
- **Close window:** `Alt+F4` or click Close button

## Menu Organization

```
Data
├── Setup Data Source...  🔌
├── Setup Map Fields...   🗺️
├── ─────────────────────
└── Sample Data...        📊  ← New!
```

The separator visually groups the data preview feature separately from configuration options.

## Future Enhancements (Potential)
- Export sample data to CSV
- Copy selected cells to clipboard
- Adjust sample size (10/25/50/100 records)
- Filter/sort capabilities
- Column width persistence
- Search within sample data

## Testing Recommendations

1. **No data source:**
   - Open Sample Data without configuring data source
   - Verify: Shows helpful message

2. **Valid data source:**
   - Configure MySQL connection
   - Open Sample Data
   - Verify: Shows up to 10 records

3. **Empty result set:**
   - Configure query that returns 0 rows (e.g., `WHERE 1=0`)
   - Open Sample Data
   - Verify: Shows "Query returned no records"

4. **Large dataset:**
   - Configure query on table with 1000+ rows
   - Open Sample Data
   - Verify: Only shows 10 records (LIMIT applied)

5. **Query with existing LIMIT:**
   - Configure query with `LIMIT 5`
   - Open Sample Data
   - Verify: Shows 5 records (respects existing LIMIT)

6. **Connection error:**
   - Configure invalid host/credentials
   - Open Sample Data
   - Verify: Shows error dialog with details

7. **Refresh functionality:**
   - Open Sample Data
   - Modify data in database
   - Click Refresh
   - Verify: Shows updated data
