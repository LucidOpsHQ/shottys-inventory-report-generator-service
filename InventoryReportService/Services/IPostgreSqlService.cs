using System.Data;

namespace InventoryReportService.Services;

public interface IPostgreSqlService
{
    Task<DataTable> GetInventoryDataAsync(string query);
}
