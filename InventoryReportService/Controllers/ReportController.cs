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
                    actual_unit_cost as ""Actual Unit Cost"",
                    actual_value as ""Actual Value""
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
