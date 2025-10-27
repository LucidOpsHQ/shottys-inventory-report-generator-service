# Inventory Report Generator Service

A .NET 8 Web API that generates Excel reports by fetching data from PostgreSQL and updating Excel templates while preserving all charts, dashboards, and formatting.

## Features

- Fetches data from PostgreSQL database using custom queries
- Updates Excel template files with fresh data
- **Preserves all Excel elements**: charts, dashboards, pivot tables, and formatting
- Returns the updated Excel file for download
- RESTful API with Swagger documentation

## Prerequisites

- .NET 8.0 SDK
- PostgreSQL database
- Excel template file (.xlsx)

## Configuration

Update `appsettings.json` with your specific settings:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=inventory_db;Username=postgres;Password=your_password"
  },
  "ExcelSettings": {
    "TemplateFilePath": "C:\\Reports\\template.xlsx",
    "SheetNameToReplace": "Data"
  }
}
```

### Configuration Parameters

- **PostgreSQL**: Connection string to your PostgreSQL database
- **TemplateFilePath**: Full path to your Excel template file
- **SheetNameToReplace**: Name of the worksheet to update with fresh data

## Running the Application

```bash
cd InventoryReportService
dotnet run
```

The API will start on:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

## API Endpoints

### 1. Generate Report (GET)

**Endpoint**: `GET /api/report/generate`

**Query Parameters**:
- `query` (optional): SQL query to fetch data. Defaults to `SELECT * FROM inventory ORDER BY id`

**Example**:
```bash
curl -X GET "https://localhost:5001/api/report/generate?query=SELECT%20*%20FROM%20inventory%20WHERE%20status='active'" -o report.xlsx
```

### 2. Generate Report (POST)

**Endpoint**: `POST /api/report/generate`

**Request Body**:
```json
{
  "query": "SELECT id, name, quantity, price FROM inventory WHERE quantity > 0"
}
```

**Example**:
```bash
curl -X POST "https://localhost:5001/api/report/generate" \
  -H "Content-Type: application/json" \
  -d "{\"query\":\"SELECT * FROM inventory WHERE active=true\"}" \
  -o report.xlsx
```

### 3. Health Check

**Endpoint**: `GET /api/report/health`

**Example**:
```bash
curl -X GET "https://localhost:5001/api/report/health"
```

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-24T10:30:00Z",
  "templateFile": "C:\\Reports\\template.xlsx",
  "sheetName": "Data"
}
```

## Swagger Documentation

Access the Swagger UI at: `https://localhost:5001/swagger`

## How It Works

1. **Request Received**: API receives a request with an optional SQL query
2. **Fetch Data**: Executes the query against PostgreSQL database
3. **Load Template**: Opens the Excel template file specified in configuration
4. **Replace Data**:
   - Clears only the data cells in the specified worksheet
   - **Preserves** all charts, formatting, formulas, and other Excel objects
   - Loads new data from PostgreSQL
   - Recalculates formulas
5. **Return File**: Returns the updated Excel file as a download

## Key Technologies

- **ASP.NET Core 8.0**: Web API framework
- **EPPlus 8.2.1**: Excel file manipulation (preserves charts and formatting)
- **Npgsql 9.0.4**: PostgreSQL data provider
- **Swagger/OpenAPI**: API documentation

## Excel Template Requirements

- Must be in `.xlsx` format (OpenXML)
- Should have a designated sheet for data (specified in `ExcelSettings.SheetNameToReplace`)
- Charts and dashboards should reference the data sheet
- Data will be replaced starting at cell A1

## Important Notes

### EPPlus License
This project uses EPPlus with a NonCommercial license. For commercial use, you must purchase an EPPlus commercial license from [EPPlus Software](https://epplussoftware.com/).

### Preserving Excel Elements
The service is specifically designed to preserve:
- Charts and graphs
- Dashboards
- Pivot tables
- Cell formatting (colors, fonts, borders)
- Named ranges
- Comments and notes
- Embedded objects

Only the **cell values and formulas** in the specified sheet are cleared and replaced.

## Error Handling

The API includes comprehensive error handling:
- Invalid queries return 400 Bad Request
- Missing template files return 500 Internal Server Error with details
- Database connection errors are logged and returned with error messages

## Security Considerations

- **SQL Injection**: The API accepts raw SQL queries. In production, consider:
  - Using parameterized queries
  - Implementing query validation
  - Restricting allowed SQL commands
  - Using stored procedures
- **File Access**: Ensure proper file system permissions
- **Authentication**: Add authentication/authorization for production use

## Troubleshooting

### Template File Not Found
Ensure the path in `appsettings.json` is correct and the file exists.

### Sheet Not Found
Verify the `SheetNameToReplace` matches exactly (case-sensitive) with a sheet name in your template.

### Database Connection Failed
Check your PostgreSQL connection string and ensure the database is accessible.

## Example PostgreSQL Setup

```sql
CREATE TABLE inventory (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255),
    quantity INTEGER,
    price DECIMAL(10, 2),
    status VARCHAR(50),
    active BOOLEAN,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO inventory (name, quantity, price, status, active) VALUES
    ('Product A', 100, 29.99, 'in_stock', true),
    ('Product B', 50, 49.99, 'in_stock', true),
    ('Product C', 0, 19.99, 'out_of_stock', false);
```

## Development

### Project Structure
```
InventoryReportService/
├── Controllers/
│   └── ReportController.cs      # API endpoints
├── Models/
│   └── ExcelSettings.cs         # Configuration model
├── Services/
│   ├── IPostgreSqlService.cs    # PostgreSQL interface
│   ├── PostgreSqlService.cs     # PostgreSQL implementation
│   ├── IExcelService.cs         # Excel interface
│   └── ExcelService.cs          # Excel implementation
├── Program.cs                   # Application entry point
└── appsettings.json            # Configuration
```

## License

This project uses EPPlus which requires a license for commercial use. The template itself is provided as-is for educational and development purposes.
