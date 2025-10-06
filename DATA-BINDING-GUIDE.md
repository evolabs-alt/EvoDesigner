# Data Binding Guide for EvoDesigner

## Overview
EvoDesigner supports dynamic data binding from MySQL databases. When configured, the system will:
- Execute your SQL query
- Print one label per database record
- Replace `{{FieldName}}` placeholders with actual data from each record
- Send all pages in a single print job

## How to Use Data Binding

### Step 1: Configure Data Source
1. Open your template in EvoDesigner
2. Click **Data Source** in the menu
3. Fill in MySQL connection details:
   - **Host**: Your MySQL server (e.g., `localhost`)
   - **Port**: MySQL port (default: `3306`)
   - **Database**: Database name
   - **Username**: MySQL username
   - **Password**: MySQL password
4. Write your SQL query:
   ```sql
   SELECT product_name, price, sku, category FROM products WHERE active = 1
   ```
5. Select **Default Printer** (for flow mode)
6. Click **Test Connection** to verify
7. Click **OK** to save

### Step 2: Add Field Placeholders to Label Elements
Use `{{ColumnName}}` syntax in your label elements:

#### Text Elements
- Content: `Product: {{product_name}}`
- Content: `Price: ${{price}}`
- Content: `Category: {{category}}`

#### QR Codes
- Content: `{{sku}}` (will encode the SKU value)
- Content: `{{product_url}}` (will encode URL from database)

#### Barcodes (EAN-13, EAN-8, Code128)
- Content: `{{barcode_value}}`

#### Multiple Fields in One Element
- Content: `{{product_name}} - ${{price}} ({{category}})`

### Step 3: Print
When you print (manually or via scheduled printing):
- Query executes automatically
- One page generated per database record
- All pages sent in **one print job** to the printer
- Each page shows the correct data from its corresponding record

## Example Template Structure

```xml
<LabelTemplate>
  <TemplateName>Product Label</TemplateName>
  <LabelWidth>100</LabelWidth>
  <LabelHeight>60</LabelHeight>
  
  <Elements>
    <LabelElement>
      <Type>Text</Type>
      <Content>{{product_name}}</Content>
      <FontSize>16</FontSize>
    </LabelElement>
    
    <LabelElement>
      <Type>QRCode</Type>
      <Content>{{sku}}</Content>
    </LabelElement>
  </Elements>
  
  <DataSource>
    <Host>localhost</Host>
    <Port>3306</Port>
    <Database>inventory</Database>
    <Username>root</Username>
    <Password>password</Password>
    <Query>SELECT product_name, sku FROM products</Query>
    <DefaultPrinter>Microsoft Print to PDF</DefaultPrinter>
  </DataSource>
</LabelTemplate>
```

## Field Mapping Examples

### Database Query:
```sql
SELECT 
  product_name,
  price,
  sku,
  category,
  description,
  barcode
FROM products 
WHERE active = 1 
ORDER BY product_name
```

### Label Template Uses:
- `{{product_name}}` → replaced with "Widget Pro"
- `{{price}}` → replaced with "29.99"
- `{{sku}}` → replaced with "WDG-PRO-001"
- `{{category}}` → replaced with "Electronics"
- `{{description}}` → replaced with "High quality widget"
- `{{barcode}}` → replaced with "1234567890123"

## Flow Mode Behavior
When `config.mode = "flow"`:
1. Application runs with system tray
2. Templates with enabled flag print every 60 seconds
3. Each scheduled print:
   - Executes the data source query
   - Prints all records in one job
   - Uses the configured Default Printer
   - Logs results to system tray notifications

## Testing Your Setup
1. Create a test template with data binding
2. Use a simple query: `SELECT * FROM your_table LIMIT 3`
3. Add text elements with `{{column_name}}` placeholders
4. Click **Print Preview** to see if data appears correctly
5. If data shows correctly, enable for scheduled printing

## Troubleshooting

### Placeholders Not Replaced
- Check that placeholder exactly matches column name (case-sensitive)
- Verify query returns the expected columns
- Use Test Connection in Data Source window

### No Data Printing
- Verify query returns rows (test in MySQL client)
- Check MySQL credentials are correct
- Ensure database/table names are correct

### Multiple Print Jobs Instead of One
- ✅ This has been fixed! All records now print in a single print job with multiple pages

## Sample Template
See `sample-databinding-template.xml` for a complete working example with:
- Product name text
- Price text
- SKU as QR code
- Category label
- MySQL query configuration
