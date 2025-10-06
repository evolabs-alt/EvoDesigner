# Data Binding Debug Instructions

## Issue: Labels printed but don't show database data

### Debugging Steps:

1. **Check if you have configured a template with a DataSource**
   - Open EvoDesigner
   - Load or create a template
   - Click "Data Source" menu
   - Fill in MySQL connection details
   - Add a SQL query (e.g., `SELECT name, price FROM products LIMIT 5`)
   - Click "Test Connection" to verify it works
   - Click OK to save

2. **Add placeholders to your label elements**
   - Add a Text element
   - Set its content to include placeholders like: `Product: {{name}}`
   - Add another text with: `Price: ${{price}}`
   - Save the template

3. **Enable the template for scheduled printing**
   - In the system tray, right-click the icon
   - Select "Template Manager"
   - Find your template
   - Check the "Enabled" checkbox
   - Click OK

4. **Test with "Run Now"**
   - Right-click system tray icon
   - Click "Run Now"
   - Check the notification

5. **Check the debug logs**
   - Look in the EvoDesigner folder for these files:
     * `run-now-debug.log` - Scheduled printing logs
     * `print-service-debug.log` - Print service logs
   
   These will show:
   - If the query executed
   - How many records were returned
   - What columns were found
   - Sample data from the first few rows
   - If placeholders were replaced
   - What content was sent to the printer

## Example Template Content

Your template XML should look like this:

```xml
<LabelTemplate>
  <TemplateName>My Product Label</TemplateName>
  <Elements>
    <LabelElement>
      <Type>Text</Type>
      <Content>Product: {{product_name}}</Content>
      <FontSize>14</FontSize>
    </LabelElement>
    <LabelElement>
      <Type>QRCode</Type>
      <Content>{{barcode}}</Content>
    </LabelElement>
  </Elements>
  <DataSource>
    <Host>localhost</Host>
    <Port>3306</Port>
    <Database>inventory</Database>
    <Username>root</Username>
    <Password>password</Password>
    <Query>SELECT product_name, barcode FROM products LIMIT 10</Query>
    <DefaultPrinter>Microsoft Print to PDF</DefaultPrinter>
  </DataSource>
</LabelTemplate>
```

## Common Issues:

### 1. Template doesn't have DataSource configured
**Solution**: Open template, click Data Source menu, configure MySQL connection and query

### 2. Label elements don't have {{placeholders}}
**Solution**: Edit each text/QR/barcode element to include {{ColumnName}} syntax

### 3. Column names don't match placeholders
**Solution**: Make sure {{FieldName}} exactly matches the column name from your SQL query (case-sensitive)

### 4. Query not executing
**Check**: 
- MySQL credentials are correct
- Database/table exists
- User has SELECT permissions
- Look in `run-now-debug.log` for error messages

### 5. Data loads but doesn't appear on labels
**Check**:
- Look in `print-service-debug.log` for replacement messages
- Verify placeholders match column names exactly
- Check if content shows "Replaced {{name}} with 'value'" messages

## Next Steps:

Run the application with these enhanced logs and:
1. Click "Run Now" in the system tray
2. Immediately check `run-now-debug.log` - you should see:
   ```
   Executing query: SELECT ...
   Query returned X records
   Columns: name, price, barcode
   Row 0: Widget Pro, 29.99, 12345
   Row 1: Gadget Plus, 49.99, 67890
   ```

3. Check `print-service-debug.log` - you should see:
   ```
   Starting batch print for X records
   DataTable has 3 columns: name, price, barcode
   Replaced {{name}} with 'Widget Pro' for record 0
   Content transformed from '{{name}}' to 'Widget Pro'
   ```

If you don't see these messages, the issue will be clear from what IS in the logs!
