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

# Create directories for volumes
RUN mkdir -p /app/Template

# Expose port
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "InventoryReportService.dll"]
