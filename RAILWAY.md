# Railway Deployment Guide

## Overview

This guide explains how to deploy the Inventory Report Service to Railway.app without using file volumes.

## Prerequisites

- Railway account (https://railway.app)
- Railway CLI (optional): `npm i -g @railway/cli`
- Git repository

## Deployment Steps

### 1. Prepare Your Repository

The Docker configuration has been updated to include all necessary files in the image:
- `appsettings.json` - Baked into the image
- `Template/` folder - Baked into the image

**Important**: Make sure your `Template` folder and Excel files are committed to git:

```bash
# Check if Template folder is in git
git add InventoryReportService/Template/
git commit -m "Add Template folder for deployment"
```

### 2. Deploy to Railway

#### Option A: Deploy from GitHub (Recommended)

1. Go to https://railway.app
2. Click **"New Project"**
3. Select **"Deploy from GitHub repo"**
4. Choose your repository
5. Railway will automatically detect the Dockerfile and build it

#### Option B: Deploy using Railway CLI

```bash
# Login to Railway
railway login

# Initialize project
railway init

# Deploy
railway up
```

### 3. Configure Environment Variables

In the Railway dashboard, add the following environment variables:

#### Required Variables

| Variable | Value | Description |
|----------|-------|-------------|
| `ConnectionStrings__PostgreSQL` | `Host=xxx;Port=xxx;Database=xxx;Username=xxx;Password=xxx` | Your PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET environment |
| `PORT` | `8080` | Port for the service (Railway will set this automatically) |

**Note**: Use double underscores (`__`) to set nested configuration values.

#### Optional Variables

| Variable | Value | Description |
|----------|-------|-------------|
| `ExcelSettings__TemplateFilePath` | `./Template/InventoryValues.xlsx` | Override template path |
| `ExcelSettings__SheetNameToReplace` | `Master Data` | Override sheet name |
| `Logging__LogLevel__Default` | `Information` | Log level |

### 4. Using Railway PostgreSQL Add-on

If you're using Railway's built-in PostgreSQL:

1. Add PostgreSQL from Railway's services
2. Railway will automatically create environment variables like:
   - `DATABASE_URL`
   - `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`

3. You can use these to construct your connection string:

```bash
# Set this in Railway environment variables
ConnectionStrings__PostgreSQL="Host=${PGHOST};Port=${PGPORT};Database=${PGDATABASE};Username=${PGUSER};Password=${PGPASSWORD}"
```

Or in Railway dashboard, use the reference format:
```
Host=${{PGHOST}};Port=${{PGPORT}};Database=${{PGDATABASE}};Username=${{PGUSER}};Password=${{PGPASSWORD}}
```

### 5. Configure Port (Important!)

Railway expects your app to listen on the `PORT` environment variable. Update your deployment:

**Option A**: Set in Railway environment variables:
```
ASPNETCORE_URLS=http://+:${PORT}
```

**Option B**: Or use Railway's variable reference:
```
ASPNETCORE_URLS=http://+:${{PORT}}
```

### 6. Verify Deployment

Once deployed, Railway will provide a public URL like:
```
https://your-service.railway.app
```

Test the endpoints:

```bash
# Health check
curl https://your-service.railway.app/api/Report/health

# Generate report
curl https://your-service.railway.app/api/Report/generate -o report.xlsx
```

## Environment Variable Examples

### Full Configuration Example

In Railway dashboard, add these variables:

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:${{PORT}}
ConnectionStrings__PostgreSQL=Host=postgres.railway.internal;Port=5432;Database=railway;Username=postgres;Password=your-password
ExcelSettings__TemplateFilePath=./Template/InventoryValues.xlsx
ExcelSettings__SheetNameToReplace=Master Data
```

## Updating Template Files

Since the Template folder is baked into the Docker image:

1. Update your template files locally
2. Commit and push changes:
   ```bash
   git add InventoryReportService/Template/
   git commit -m "Update Excel template"
   git push
   ```
3. Railway will automatically rebuild and redeploy

## Updating Configuration

To update configuration without redeploying:

1. Go to Railway dashboard → Your project → Variables
2. Update environment variables
3. Railway will automatically restart your service

## Troubleshooting

### Check Logs

```bash
# Using Railway CLI
railway logs

# Or view in Railway dashboard under "Deployments" tab
```

### Common Issues

#### 1. Port binding error
**Error**: Application not responding
**Solution**: Ensure `ASPNETCORE_URLS=http://+:${{PORT}}` is set

#### 2. Database connection failed
**Error**: Cannot connect to PostgreSQL
**Solution**: Verify `ConnectionStrings__PostgreSQL` format and credentials

#### 3. Template file not found
**Error**: Template file not found
**Solution**: Ensure Template folder is committed to git and in the image

```bash
# Check if Template is in git
git ls-files | grep Template

# If not, add it
git add InventoryReportService/Template/ -f
git commit -m "Add Template folder"
```

### Debug Container Locally

Test the Railway build locally:

```bash
# Build the same image Railway will build
docker build -t inventory-report-service .

# Run with Railway-like environment
docker run -d \
  -p 8080:8080 \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e ConnectionStrings__PostgreSQL="Host=your-host;Port=5432;Database=db;Username=user;Password=pass" \
  inventory-report-service

# Check if Template files are in the image
docker exec -it <container-id> ls -la /app/Template
```

## Monitoring

Railway provides:
- **Metrics**: CPU, Memory, Network usage
- **Logs**: Real-time application logs
- **Deployments**: Build and deployment history

Access these from your project dashboard.

## Cost Optimization

Railway charges based on:
- Build time
- Runtime hours
- Memory usage

To optimize:
- Use multi-stage builds (already configured)
- Set appropriate resource limits in Railway
- Monitor usage in the Railway dashboard

## Security Best Practices

1. **Never commit sensitive data** to git (already configured in .gitignore)
2. **Use environment variables** for all secrets
3. **Rotate credentials** regularly
4. **Enable Railway's built-in security features**
5. **Use HTTPS** for all endpoints (Railway provides this automatically)

## Next Steps

- Set up custom domain in Railway
- Configure health checks
- Set up monitoring/alerting
- Enable automatic deployments from main branch
