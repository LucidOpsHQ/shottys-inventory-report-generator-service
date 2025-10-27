# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["InventoryReportService/InventoryReportService.csproj", "InventoryReportService/"]
RUN dotnet restore "InventoryReportService/InventoryReportService.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/InventoryReportService"
RUN dotnet build "InventoryReportService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "InventoryReportService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy Template folder into the image
COPY InventoryReportService/Template /app/Template

# Copy appsettings.json (base configuration, sensitive data via env vars)
COPY InventoryReportService/appsettings.json /app/appsettings.json

# Expose port (Railway will override with its own PORT variable)
EXPOSE 8080

# Set environment variables
# Note: ASPNETCORE_URLS will be set via Railway environment variable
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "InventoryReportService.dll"]
