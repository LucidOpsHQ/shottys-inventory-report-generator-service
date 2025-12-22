using System.Data;
using Npgsql;

namespace InventoryReportService.Services;

public class PostgreSqlService : IPostgreSqlService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlService> _logger;

    public PostgreSqlService(IConfiguration configuration, ILogger<PostgreSqlService> logger)
    {
        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new ArgumentNullException(nameof(configuration), "PostgreSQL connection string is not configured");
        _logger = logger;
    }

    public async Task<DataTable> GetInventoryDataAsync(string query)
    {
        try
        {
            _logger.LogInformation("Executing query to fetch inventory data");

            var dataTable = new DataTable();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            dataTable.Load(reader);

            _logger.LogInformation("Successfully fetched {RowCount} rows from database", dataTable.Rows.Count);

            return dataTable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching data from PostgreSQL");
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetGoodsAveragePricesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching goods average prices");

            var goodsPrices = new Dictionary<string, decimal>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            const string query = "SELECT sku, average_price FROM goods";
            await using var command = new NpgsqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var sku = reader.GetString(0);
                var averagePrice = reader.GetDecimal(1);
                goodsPrices[sku] = averagePrice;
            }

            _logger.LogInformation("Successfully fetched {Count} goods with average prices", goodsPrices.Count);

            return goodsPrices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching goods average prices from PostgreSQL");
            throw;
        }
    }
}
