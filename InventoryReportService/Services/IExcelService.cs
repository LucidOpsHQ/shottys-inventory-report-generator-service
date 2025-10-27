using System.Data;

namespace InventoryReportService.Services;

public interface IExcelService
{
    Task<byte[]> ReplaceSheetDataAsync(string templateFilePath, string sheetName, DataTable data);
}
