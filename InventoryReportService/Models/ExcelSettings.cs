namespace InventoryReportService.Models;

public class ExcelSettings
{
    public string TemplateFilePath { get; set; } = string.Empty;
    public string SheetNameToReplace { get; set; } = string.Empty;
}
