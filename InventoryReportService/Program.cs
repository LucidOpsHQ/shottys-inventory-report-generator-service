using InventoryReportService.Models;
using InventoryReportService.Services;
using OfficeOpenXml;

// Configure EPPlus license (required for EPPlus 8+)
// For personal/non-commercial use - update with your name or organization
ExcelPackage.License.SetNonCommercialPersonal("Shottys");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure ExcelSettings from appsettings.json
builder.Services.Configure<ExcelSettings>(
    builder.Configuration.GetSection("ExcelSettings"));

// Configure SupabaseSettings from appsettings.json
builder.Services.Configure<SupabaseSettings>(
    builder.Configuration.GetSection("SupabaseSettings"));

// Configure S3Settings from appsettings.json
builder.Services.Configure<S3Settings>(
    builder.Configuration.GetSection("S3Settings"));

// Register application services
builder.Services.AddScoped<IPostgreSqlService, PostgreSqlService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<IS3StorageService, S3StorageService>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
