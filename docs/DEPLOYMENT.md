# Deployment Guide

Production deployment guide за FirstMake Agent.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Environment Setup](#environment-setup)
- [Database Configuration](#database-configuration)
- [Application Build](#application-build)
- [Deployment Options](#deployment-options)
  - [Self-Hosted Server](#self-hosted-server)
  - [Docker Deployment](#docker-deployment)
  - [Cloud Deployment](#cloud-deployment)
- [Configuration](#configuration)
- [Monitoring](#monitoring)
- [Backup & Recovery](#backup--recovery)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### System Requirements

- **CPU**: 4+ cores (препоръчва се за LP optimization)
- **RAM**: 8GB minimum, 16GB препоръчва се
- **Disk**: 10GB+ (за application + database + logs)
- **OS**: Linux (Ubuntu 22.04/24.04), Windows Server 2019+, macOS

### Software Requirements

- **.NET 8 Runtime** (ASP.NET Core Runtime)
- **SQLite3** (обикновено вграден)
- **Nginx** or **Apache** (за reverse proxy)
- **Systemd** (за Linux service management)

## Environment Setup

### 1. Create Application User

```bash
# Linux
sudo useradd -r -s /bin/false firstmake
sudo mkdir -p /var/www/firstmake
sudo chown firstmake:firstmake /var/www/firstmake
```

### 2. Install .NET Runtime

```bash
# Ubuntu 24.04
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0
```

### 3. Install Tesseract OCR (за AI Gateway)

```bash
# Ubuntu
sudo apt-get install -y tesseract-ocr tesseract-ocr-bul tesseract-ocr-eng

# Verify
tesseract --version
```

## Production Deployment Checklist

Използвайте този checklist преди production deployment:

### Pre-Deployment
- [ ] Code review завършен
- [ ] All tests passed (dotnet test)
- [ ] Security audit изпълнен (secret scan)
- [ ] Dependencies актуализирани
- [ ] Environment variables подготвени
- [ ] SSL certificates готови
- [ ] Backup strategy дефинирана

### Build & Package
- [ ] Backend built: `dotnet publish -c Release`
- [ ] Frontend built: `npm run build`
- [ ] Docker images built (ако се използват)
- [ ] Version tag създаден

### Configuration
- [ ] Production appsettings.json files подготвени
- [ ] Environment variables настроени
- [ ] Database connection string валиден
- [ ] BG GPT API key конфигуриран
- [ ] File paths и permissions проверени

### Deployment
- [ ] Systemd services инсталирани
- [ ] Nginx reverse proxy конфигуриран
- [ ] SSL/TLS enableнат
- [ ] Firewall rules настроени
- [ ] Health checks работят

### Post-Deployment
- [ ] Services started и running
- [ ] Logs проверени за errors
- [ ] Database migrations приложени
- [ ] Backup schedule активиран
- [ ] Monitoring setup завършен

### Verification
- [ ] API healthcheck отговаря: `/api/healthz`
- [ ] AI Gateway healthcheck отговаря: `/ai/healthz`
- [ ] Frontend зарежда правилно
- [ ] File upload работи
- [ ] Database queries успешни
- [ ] SSL certificate валиден

### Documentation
- [ ] Deployment notes актуализирани
- [ ] Team нотифициран
- [ ] Rollback plan подготвен

## Database Configuration

### SQLite Setup

```bash
# Create database directory
sudo mkdir -p /var/lib/firstmake/data
sudo chown firstmake:firstmake /var/lib/firstmake/data

# Set connection string
export ConnectionStrings__DefaultConnection="Data Source=/var/lib/firstmake/data/firstmake.db"
```

### Run Migrations

```bash
cd /var/www/firstmake/Api
dotnet ef database update --connection "Data Source=/var/lib/firstmake/data/firstmake.db"
```

### Database Permissions

```bash
sudo chmod 660 /var/lib/firstmake/data/firstmake.db
sudo chown firstmake:firstmake /var/lib/firstmake/data/firstmake.db
```

## Application Build

### 1. Build Backend

```bash
# Clone repository
git clone https://github.com/BlagoyKozarev/First-make.git
cd First-make

# Build API
cd src/Api
dotnet publish -c Release -o /var/www/firstmake/Api

# Build AiGateway
cd ../AiGateway
dotnet publish -c Release -o /var/www/firstmake/AiGateway
```

### 2. Build Frontend

```bash
cd src/UI
npm ci --production
npm run build

# Copy build output
sudo cp -r dist /var/www/firstmake/UI
```

### 3. Set Permissions

```bash
sudo chown -R firstmake:firstmake /var/www/firstmake
sudo chmod -R 755 /var/www/firstmake
```

## Deployment Options

### Self-Hosted Server

#### Systemd Services

**API Service** (`/etc/systemd/system/firstmake-api.service`):

```ini
[Unit]
Description=FirstMake API Service
After=network.target

[Service]
Type=notify
User=firstmake
Group=firstmake
WorkingDirectory=/var/www/firstmake/Api
ExecStart=/usr/bin/dotnet /var/www/firstmake/Api/Api.dll
Restart=on-failure
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=ConnectionStrings__DefaultConnection=Data Source=/var/lib/firstmake/data/firstmake.db

[Install]
WantedBy=multi-user.target
```

**AI Gateway Service** (`/etc/systemd/system/firstmake-ai.service`):

```ini
[Unit]
Description=FirstMake AI Gateway Service
After=network.target

[Service]
Type=notify
User=firstmake
Group=firstmake
WorkingDirectory=/var/www/firstmake/AiGateway
ExecStart=/usr/bin/dotnet /var/www/firstmake/AiGateway/AiGateway.dll
Restart=on-failure
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5001
Environment=BgGpt__ApiKey=YOUR_API_KEY_HERE
Environment=Tesseract__DataPath=/usr/share/tesseract-ocr/5/tessdata

[Install]
WantedBy=multi-user.target
```

**Enable and Start Services:**

```bash
sudo systemctl daemon-reload
sudo systemctl enable firstmake-api
sudo systemctl enable firstmake-ai
sudo systemctl start firstmake-api
sudo systemctl start firstmake-ai

# Check status
sudo systemctl status firstmake-api
sudo systemctl status firstmake-ai

# View logs
sudo journalctl -u firstmake-api -f
```

#### Nginx Reverse Proxy

**Configuration** (`/etc/nginx/sites-available/firstmake`):

```nginx
# API and AI Gateway
upstream firstmake_api {
    server localhost:5000;
}

upstream firstmake_ai {
    server localhost:5001;
}

# Main server
server {
    listen 80;
    server_name firstmake.example.com;

    # Redirect to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name firstmake.example.com;

    # SSL Configuration
    ssl_certificate /etc/letsencrypt/live/firstmake.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/firstmake.example.com/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Client max body size (for file uploads)
    client_max_body_size 100M;

    # Frontend (React app)
    location / {
        root /var/www/firstmake/UI;
        try_files $uri $uri/ /index.html;
        
        # Cache static assets
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # API
    location /api/ {
        rewrite ^/api/(.*) /$1 break;
        proxy_pass http://firstmake_api;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Timeout for long-running operations
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }

    # AI Gateway
    location /ai/ {
        rewrite ^/ai/(.*) /$1 break;
        proxy_pass http://firstmake_ai;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Larger timeout for OCR operations
        proxy_read_timeout 600s;
        proxy_connect_timeout 75s;
    }

    # Healthcheck
    location /health {
        access_log off;
        proxy_pass http://firstmake_api/healthz;
    }
}
```

**Enable Site:**

```bash
sudo ln -s /etc/nginx/sites-available/firstmake /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Docker Deployment

#### Production Docker Compose

**docker-compose.prod.yml:**

```yaml
version: '3.8'

services:
  api:
    build:
      context: .
      dockerfile: src/Api/Dockerfile
    restart: always
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/firstmake.db
    volumes:
      - ./data:/data
      - ./logs:/logs
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  ai-gateway:
    build:
      context: .
      dockerfile: src/AiGateway/Dockerfile
    restart: always
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - BgGpt__ApiKey=${BGGPT_API_KEY}
      - Tesseract__DataPath=/usr/share/tesseract-ocr/5/tessdata
    volumes:
      - ./logs:/logs

  nginx:
    image: nginx:alpine
    restart: always
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/ssl:ro
      - ./src/UI/dist:/usr/share/nginx/html:ro
    depends_on:
      - api
      - ai-gateway
```

**Deploy:**

```bash
# Set environment variables
export BGGPT_API_KEY="your-api-key"

# Build and start
docker-compose -f docker-compose.prod.yml build
docker-compose -f docker-compose.prod.yml up -d

# View logs
docker-compose -f docker-compose.prod.yml logs -f

# Stop
docker-compose -f docker-compose.prod.yml down
```

### Cloud Deployment

#### Azure App Service

```bash
# Login
az login

# Create resource group
az group create --name firstmake-rg --location westeurope

# Create App Service Plan
az appservice plan create \
  --name firstmake-plan \
  --resource-group firstmake-rg \
  --sku P1V2 \
  --is-linux

# Create Web App (API)
az webapp create \
  --resource-group firstmake-rg \
  --plan firstmake-plan \
  --name firstmake-api \
  --runtime "DOTNET|8.0"

# Deploy
dotnet publish -c Release
cd src/Api/bin/Release/net8.0/publish
zip -r ../deploy.zip .
az webapp deployment source config-zip \
  --resource-group firstmake-rg \
  --name firstmake-api \
  --src ../deploy.zip
```

#### AWS EC2

```bash
# Launch EC2 instance (Ubuntu 24.04)
aws ec2 run-instances \
  --image-id ami-0c55b159cbfafe1f0 \
  --count 1 \
  --instance-type t3.medium \
  --key-name firstmake-key \
  --security-group-ids sg-12345678 \
  --user-data file://cloud-init.sh

# SSH and deploy
ssh -i firstmake-key.pem ubuntu@ec2-ip-address
# Follow self-hosted deployment steps
```

## Configuration

### Production appsettings.json

**Api** (`/var/www/firstmake/Api/appsettings.Production.json`):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/firstmake/data/firstmake.db"
  },
  "ExportSettings": {
    "TempDirectory": "/var/tmp/firstmake",
    "MaxConcurrentExports": 5
  }
}
```

**AiGateway** (`/var/www/firstmake/AiGateway/appsettings.Production.json`):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "BgGpt": {
    "BaseUrl": "https://api.raicommerce.net/v1",
    "ApiKey": "REPLACE_WITH_ENV_VAR",
    "Model": "gpt-4o-mini",
    "Temperature": 0.0,
    "MaxTokens": 4000,
    "TimeoutSeconds": 60
  },
  "Tesseract": {
    "DataPath": "/usr/share/tesseract-ocr/5/tessdata",
    "Languages": "bul+eng",
    "PageSegMode": 3
  }
}
```

### Environment Variables

```bash
# /etc/environment or .env file
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection="Data Source=/var/lib/firstmake/data/firstmake.db"
BgGpt__ApiKey="your-production-api-key"
```

## Monitoring

### Logging

**Configure File Logging** (using Serilog):

```bash
# Install Serilog
cd src/Api
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
```

**appsettings.Production.json:**

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/firstmake/api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### Health Checks

Monitor с external service (e.g., UptimeRobot):

```bash
# Healthcheck endpoints
curl https://firstmake.example.com/api/healthz
curl https://firstmake.example.com/ai/healthz
```

### Metrics Dashboard

Използвайте built-in `/observations/metrics` endpoint или integrate Prometheus:

```bash
# Install Prometheus exporter
dotnet add package prometheus-net.AspNetCore
```

## Backup & Recovery

### Database Backup

```bash
# Automated backup script
#!/bin/bash
DB_PATH="/var/lib/firstmake/data/firstmake.db"
BACKUP_DIR="/var/backups/firstmake"
DATE=$(date +%Y%m%d_%H%M%S)

# Create backup
sqlite3 $DB_PATH ".backup '$BACKUP_DIR/firstmake_$DATE.db'"

# Compress
gzip "$BACKUP_DIR/firstmake_$DATE.db"

# Retain last 30 days
find $BACKUP_DIR -name "*.db.gz" -mtime +30 -delete
```

**Cron Job:**

```bash
# /etc/cron.d/firstmake-backup
0 2 * * * firstmake /usr/local/bin/firstmake-backup.sh
```

### Restore

```bash
# Stop services
sudo systemctl stop firstmake-api

# Restore database
gunzip -c /var/backups/firstmake/firstmake_20250113_020000.db.gz > /var/lib/firstmake/data/firstmake.db

# Set permissions
sudo chown firstmake:firstmake /var/lib/firstmake/data/firstmake.db

# Start services
sudo systemctl start firstmake-api
```

## Troubleshooting

### Service Won't Start

```bash
# Check logs
sudo journalctl -u firstmake-api -n 100

# Verify permissions
ls -la /var/www/firstmake/Api
ls -la /var/lib/firstmake/data

# Test manually
cd /var/www/firstmake/Api
dotnet Api.dll
```

### High Memory Usage

```bash
# Check memory
free -h

# Restart services
sudo systemctl restart firstmake-api

# Configure memory limits in systemd
[Service]
MemoryLimit=2G
```

### Database Locked

```bash
# Check connections
lsof /var/lib/firstmake/data/firstmake.db

# Kill stale connections
sudo systemctl restart firstmake-api
```

### File Upload Fails

```bash
# Check Nginx client_max_body_size
sudo nano /etc/nginx/sites-available/firstmake

# Verify disk space
df -h
```

---

**Last Updated:** October 28, 2025

**Version:** 1.0.0

## Quick Reference

### Essential Commands

```bash
# Check service status
sudo systemctl status firstmake-api firstmake-ai

# View logs
sudo journalctl -u firstmake-api -f

# Restart services
sudo systemctl restart firstmake-api firstmake-ai

# Test endpoints
curl http://localhost:5000/healthz
curl http://localhost:5001/healthz

# Database backup
sqlite3 /var/lib/firstmake/data/firstmake.db ".backup /tmp/backup.db"
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Service won't start | Check logs: `journalctl -u firstmake-api` |
| 502 Bad Gateway | Verify backend services running |
| File upload fails | Check `client_max_body_size` in Nginx |
| Database locked | Restart API service |
| High memory | Configure memory limits in systemd |

### Support

- GitHub Issues: https://github.com/GitRaicommerce/First-make/issues
- Documentation: https://github.com/GitRaicommerce/First-make/tree/main/docs

---

**Production Deployment Guide completed. Review checklist before deployment.**
