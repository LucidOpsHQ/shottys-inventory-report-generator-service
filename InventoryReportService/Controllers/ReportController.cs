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
    private readonly ExcelSettings _excelSettings;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IPostgreSqlService postgreSqlService,
        IExcelService excelService,
        IOptions<ExcelSettings> excelSettings,
        ILogger<ReportController> logger)
    {
        _postgreSqlService = postgreSqlService;
        _excelService = excelService;
        _excelSettings = excelSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates an inventory report by fetching data from PostgreSQL and updating an Excel template
    /// </summary>
    /// <param name="query">SQL query to fetch data from PostgreSQL. If not provided, uses a default query.</param>
    /// <returns>Excel file with updated data</returns>
    [HttpGet("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
                    actual_unit_cost as ""Actual Unit Cost"",
                    actual_value as ""Actual Value""
                FROM inventory_cost
                ORDER BY date";

            // Fetch data from PostgreSQL
            var data = await _postgreSqlService.GetInventoryDataAsync(sqlQuery);

            if (data.Rows.Count == 0)
            {
                _logger.LogWarning("No data returned from database query");
                return BadRequest("No data found for the specified query");
            }

            // Replace sheet data in Excel template
            var excelBytes = await _excelService.ReplaceSheetDataAsync(
                _excelSettings.TemplateFilePath,
                _excelSettings.SheetNameToReplace,
                data);

            // Return the Excel file
            var fileName = $"InventoryReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

            _logger.LogInformation("Report generated successfully: {FileName}", fileName);

            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates an inventory report using a POST request with query in the body
    /// </summary>
    /// <param name="request">Request containing the SQL query</param>
    /// <returns>Excel file with updated data</returns>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateReportPost([FromBody] ReportRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        return await GenerateReport(request.Query);
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
