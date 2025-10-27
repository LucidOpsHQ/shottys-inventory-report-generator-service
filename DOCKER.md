# Docker Setup Guide

## Overview

This service is containerized using Docker with external volumes for configuration and template files.

## Volumes

The following volumes are declared in the Dockerfile:

- `/app/appsettings.json` - Application configuration file
- `/app/Template` - Excel template files directory

## Quick Start

### Option 1: Using Docker Compose (Recommended)

```bash
# Build and start the service
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the service
docker-compose down
```

### Option 2: Using Docker CLI

```bash
# Build the image
docker build -t inventory-report-service .

# Run the container with volumes
docker run -d \
  --name inventory-report-service \
  -p 8080:8080 \
  -p 8081:8081 \
  -v $(pwd)/InventoryReportService/appsettings.json:/app/appsettings.json:ro \
  -v $(pwd)/InventoryReportService/Template:/app/Template:ro \
  inventory-report-service
```

## Configuration

### appsettings.json

Create or modify `InventoryReportService/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "PostgreSQL": "Host=your-host;Port=5432;Database=your-db;Username=user;Password=pass"
  },
  "ExcelSettings": {
    "TemplateFilePath": "./Template/InventoryValues.xlsx",
    "SheetNameToReplace": "Master Data"
  }
}
```

### Template Files

Place your Excel template files in the `InventoryReportService/Template` directory:

```
InventoryReportService/
└── Template/
    └── InventoryValues.xlsx
```

## Updating Configuration

Since the volumes are mounted as read-only (`:ro`), you can update the configuration on the host:

1. Edit `InventoryReportService/appsettings.json`
2. Restart the container:
   ```bash
   docker-compose restart
   # or
   docker restart inventory-report-service
   ```

## Port Mapping

- **8080** - HTTP endpoint (mapped to host:8080)
- **8081** - HTTPS endpoint (mapped to host:8081)

## Health Check

Test the service is running:

```bash
curl http://localhost:8080/api/Report/health
```

## Generate Report

```bash
curl http://localhost:8080/api/Report/generate -o report.xlsx
```

## Troubleshooting

### View container logs
```bash
docker-compose logs -f
# or
docker logs -f inventory-report-service
```

### Access container shell
```bash
docker exec -it inventory-report-service /bin/bash
```

### Check mounted volumes
```bash
docker exec inventory-report-service ls -la /app
docker exec inventory-report-service ls -la /app/Template
```

### Rebuild after code changes
```bash
docker-compose up -d --build
```

## Environment Variables

You can override environment variables in `docker-compose.yml`:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - ASPNETCORE_URLS=http://+:8080
```

## Security Notes

- Keep `appsettings.json` out of version control (already in .gitignore)
- Use environment variables for sensitive data in production
- Consider using Docker secrets or Azure Key Vault for credentials
