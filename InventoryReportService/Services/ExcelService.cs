using System.Data;
using OfficeOpenXml;

namespace InventoryReportService.Services;

public class ExcelService : IExcelService
{
    private readonly ILogger<ExcelService> _logger;

    public ExcelService(ILogger<ExcelService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ReplaceSheetDataAsync(string templateFilePath, string sheetName, DataTable data)
    {
        try
        {
            _logger.LogInformation("Loading Excel template from {FilePath}", templateFilePath);

            if (!File.Exists(templateFilePath))
            {
                throw new FileNotFoundException($"Template file not found: {templateFilePath}");
            }

            // Create a copy of the template file to work with
            var fileBytes = await File.ReadAllBytesAsync(templateFilePath);

            using var memoryStream = new MemoryStream(fileBytes);
            using var package = new ExcelPackage(memoryStream);

            var worksheet = package.Workbook.Worksheets[sheetName];
            if (worksheet == null)
            {
                throw new InvalidOperationException($"Worksheet '{sheetName}' not found in the template");
            }

            _logger.LogInformation("Found worksheet '{SheetName}'. Replacing data...", sheetName);

            // Find the used range for data (excluding charts and other objects)
            // We'll clear only the cell data, not the entire worksheet
            var dimension = worksheet.Dimension;
            if (dimension != null)
            {
                // Clear only cell values and formulas, preserving formatting
                // This approach keeps charts, pivot tables, and other objects intact
                for (int row = 1; row <= dimension.End.Row; row++)
                {
                    for (int col = 1; col <= dimension.End.Column; col++)
                    {
                        var cell = worksheet.Cells[row, col];
                        // Clear value but keep formatting
                        cell.Value = null;
                        cell.Formula = string.Empty;
                    }
                }
            }

            // Load the new data from DataTable starting at A1
            // LoadFromDataTable preserves the worksheet structure including charts
            if (data.Rows.Count > 0)
            {
                worksheet.Cells["A1"].LoadFromDataTable(data, true);
                _logger.LogInformation("Loaded {RowCount} rows and {ColumnCount} columns into worksheet",
                    data.Rows.Count, data.Columns.Count);
            }
            else
            {
                _logger.LogWarning("No data to load into worksheet");
            }

            // Calculate formulas if needed
            package.Workbook.Calculate();

            // Save to memory stream
            var outputStream = new MemoryStream();
            await package.SaveAsAsync(outputStream);

            _logger.LogInformation("Successfully updated Excel file");

            return outputStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Excel file");
            throw;
        }
    }
}
