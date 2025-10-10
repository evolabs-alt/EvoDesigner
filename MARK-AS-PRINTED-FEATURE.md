# Mark Records as Printed Feature

## Overview
This feature allows the scheduler to automatically mark database records as printed after successful label printing in FLOW mode. This prevents the same records from being printed again on subsequent scheduler runs.

## Feature Implementation

### 1. Data Model Changes
**File**: `Models/LabelTemplate.cs`
- Added `UpdateQuery` property to `DataSourceConfig` class
- This stores the SQL UPDATE statement with placeholder syntax
- Example: `UPDATE labels SET printed = 1 WHERE id = {label_id}`

### 2. UI Changes
**File**: `UI/DataSourceWindow.xaml`
- Added "Update Query (Mark as Printed)" section in the Data Source configuration window
- Includes help text explaining placeholder syntax: `{field_name}`
- Example shown in UI: `UPDATE labels SET printed = 1 WHERE id = {label_id}`

**File**: `UI/DataSourceWindow.xaml.cs`
- Added `UpdateQuery` property to read from `UpdateQueryBox`
- Modified `LoadFromDataSource()` to load saved update query
- Modified `CreateDataSourceConfig()` to include `UpdateQuery` when saving

### 3. Scheduler Logic
**File**: `Services/ScheduledPrintingService.cs`

**New Methods**:
- `MarkRecordsAsPrinted(LabelTemplate template, DataTable dataTable)`: Executes the update query for all successfully printed records
- `ReplacePlaceholders(string query, DataRow row, DataTable dataTable)`: Replaces `{field_name}` placeholders with actual values from each record

**Modified Methods**:
- `SendPrintJob()`: Now calls `MarkRecordsAsPrinted()` after successful print completion

**Error Handling**:
- Errors during update execution are logged to `run-now-debug.log`
- Failed updates do NOT stop the scheduler
- Individual record update failures are logged separately
- Success/failure count is reported in logs

### 4. Database Service
**File**: `Services/MySqlService.cs`
- Added `ExecuteNonQuery(string query)` method for executing UPDATE statements
- Returns the number of affected rows

## How It Works

### User Configuration
1. Open Data Source configuration window
2. Enter SQL SELECT query to fetch records
3. Enter UPDATE query with placeholders, e.g.:
   ```sql
   UPDATE labels SET printed = 1 WHERE id = {label_id}
   ```
4. Save template - the UPDATE query is stored in the XML file

### Scheduler Execution Flow
1. Scheduler runs at configured interval (default: every 60 seconds)
2. Loads enabled templates
3. Executes SELECT query to get unprinted records
4. Prints all records
5. **After successful print**, executes UPDATE query for each record:
   - Replaces `{label_id}` with actual value from the record (e.g., `56`)
   - Executes: `UPDATE labels SET printed = 1 WHERE id = 56`
   - Logs success or failure for each update

### Placeholder Replacement
- Placeholders use curly braces: `{field_name}`
- Field names must match column names from the SELECT query
- Multiple placeholders supported in one UPDATE statement
- Example with multiple fields:
  ```sql
  UPDATE labels 
  SET printed = 1, printed_at = NOW(), printed_by = '{username}' 
  WHERE id = {label_id}
  ```

## Testing Instructions

### Test Scenario 1: Basic Update
1. Create a test table:
   ```sql
   CREATE TABLE labels (
       id INT PRIMARY KEY AUTO_INCREMENT,
       content VARCHAR(255),
       printed TINYINT DEFAULT 0
   );
   
   INSERT INTO labels (content) VALUES ('Label 1'), ('Label 2'), ('Label 3');
   ```

2. Configure template:
   - SELECT query: `SELECT * FROM labels WHERE printed = 0`
   - UPDATE query: `UPDATE labels SET printed = 1 WHERE id = {id}`

3. Enable scheduler and wait for execution
4. Verify in database: `SELECT * FROM labels;`
   - All printed records should have `printed = 1`

### Test Scenario 2: Error Handling
1. Use invalid UPDATE query (e.g., wrong table name)
2. Check `run-now-debug.log` for error messages
3. Verify scheduler continues running despite errors

### Test Scenario 3: Multiple Fields
1. Use UPDATE query with multiple placeholders:
   ```sql
   UPDATE labels SET printed = 1, last_user = '{user}' WHERE id = {id}
   ```
2. Verify all placeholders are correctly replaced

## Logging
All update operations are logged to `run-now-debug.log` with timestamps:
- `[TIMESTAMP] Executing update query for N records`
- `[TIMESTAMP] Update complete: X success, Y failed`
- `[TIMESTAMP] ERROR marking record Z: error message`

## Important Notes
1. **SQL Injection Prevention**: Placeholder values are NOT escaped. Ensure your database user has limited permissions.
2. **Atomic Operations**: Each record's UPDATE is executed separately. Partial failures are possible.
3. **Performance**: For large batches, consider using a single batch UPDATE if your database supports it.
4. **Template Persistence**: UPDATE query is saved in the XML template file and persists across application restarts.

## Future Enhancements
- [ ] Add batch UPDATE support for better performance
- [ ] Add SQL parameter binding for security
- [ ] Add transaction support for atomic batch updates
- [ ] Add retry logic for failed updates
- [ ] Add UI to test UPDATE query before saving
