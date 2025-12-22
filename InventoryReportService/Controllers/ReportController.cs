using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using InventoryReportService.Models;
using InventoryReportService.Services;

namespace InventoryReportService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly IExcelService _excelService;
    private readonly ISupabaseStorageService _supabaseStorageService;
    private readonly ExcelSettings _excelSettings;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IPostgreSqlService postgreSqlService,
        IExcelService excelService,
        ISupabaseStorageService supabaseStorageService,
        IOptions<ExcelSettings> excelSettings,
        ILogger<ReportController> logger)
    {
        _postgreSqlService = postgreSqlService;
        _excelService = excelService;
        _supabaseStorageService = supabaseStorageService;
        _excelSettings = excelSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates an inventory report by fetching data from PostgreSQL and updating an Excel template
    /// </summary>
    /// <param name="query">SQL query to fetch data from PostgreSQL. If not provided, uses a default query.</param>
    /// <returns>Redirect to the uploaded file in Supabase storage</returns>
    [HttpGet("generate")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateReport([FromQuery] string? query = null)
    {
        try
        {
            _logger.LogInformation("Report generation requested");

            // Default query if none provided
            var sqlQuery = query ?? @"
                SELECT
                    date as ""Date"",
                    area as ""Area"",
                    item as ""Item"",
                    key as ""Description"",
                    gl_group as ""GLGroup"",
                    type as ""Type"",
                    qty as ""Qty"",
                    unit as ""Unit"",
                    standard_unit_cost as ""Standard Unit Cost"",
                    standard_value as ""Standard Value""
                FROM inventory_cost
                WHERE area != 'MARKETING'
                ORDER BY date";

            // Fetch data from PostgreSQL
            var data = await _postgreSqlService.GetInventoryDataAsync(sqlQuery);

            if (data.Rows.Count == 0)
            {
                _logger.LogWarning("No data returned from database query");
                return BadRequest("No data found for the specified query");
            }

            // Fetch goods average prices
            var goodsPrices = await _postgreSqlService.GetGoodsAveragePricesAsync();

            // Modify data in-memory: replace standard_value with average_price and recalculate standard_unit_cost
            if (goodsPrices.Count > 0 && data.Columns.Contains("Item") && data.Columns.Contains("Standard Value") && data.Columns.Contains("Standard Unit Cost") && data.Columns.Contains("Qty"))
            {
                // Make columns writable (they may be read-only when loaded from database)
                var standardValueColumn = data.Columns["Standard Value"];
                var standardUnitCostColumn = data.Columns["Standard Unit Cost"];
                if (standardValueColumn != null) standardValueColumn.ReadOnly = false;
                if (standardUnitCostColumn != null) standardUnitCostColumn.ReadOnly = false;

                int updatedCount = 0;
                foreach (DataRow row in data.Rows)
                {
                    var skuValue = row["Item"];
                    if (skuValue != null && skuValue != DBNull.Value)
                    {
                        var sku = skuValue.ToString();
                        if (!string.IsNullOrWhiteSpace(sku) && goodsPrices.TryGetValue(sku, out var averagePrice))
                        {
                            // Replace standard_value with average_price
                            row["Standard Unit Cost"] = averagePrice;

                            // Recalculate standard_unit_cost = average_price / qty
                            // qty is always numeric (can be negative, 0, or null) - PostgreSQL may return as Single/Float
                            var qtyValue = row["Qty"];
                            if (qtyValue != null && qtyValue != DBNull.Value)
                            {
                                var qty = Convert.ToDecimal(qtyValue);
                                if (qty != 0)
                                {
                                    var newStandardValue = averagePrice * qty;
                                    row["Standard Value"] = newStandardValue;
                                    updatedCount++;
                                }
                            }
                        }
                    }
                }
                _logger.LogInformation("Updated {Count} rows with goods average prices", updatedCount);
            }
            else
            {
                _logger.LogWarning("Skipping goods price update: missing required columns or no goods data available");
            }

            // Replace sheet data in Excel template
            var excelBytes = await _excelService.ReplaceSheetDataAsync(
                _excelSettings.TemplateFilePath,
                _excelSettings.SheetNameToReplace,
                data);

            // Generate unique file name
            var fileName = $"InventoryReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

            // Upload to Supabase storage
            var publicUrl = await _supabaseStorageService.UploadFileAsync(
                excelBytes,
                fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            _logger.LogInformation("Report generated and uploaded successfully: {FileName}, URL: {Url}", fileName, publicUrl);

            // Return Found (302) redirect to the public URL
            return Redirect(publicUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            TemplateFile = _excelSettings.TemplateFilePath,
            SheetName = _excelSettings.SheetNameToReplace
        });
    }
}

public class ReportRequest
{
    public string Query { get; set; } = string.Empty;
}
