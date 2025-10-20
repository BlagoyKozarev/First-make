# FirstMake Agent - Production Deployment Guide

Deployment configurations and scripts for production environments.

## 📂 Directory Structure

```
deployment/
├── docker-compose.prod.yml   # Production Docker Compose configuration
├── deploy.sh                 # Automated deployment script
├── .env.example              # Environment variables template
├── systemd/                  # Systemd service files
│   ├── firstmake-api.service
│   └── firstmake-aigateway.service
├── nginx/                    # Nginx configurations
│   └── conf.d/
└── monitoring/               # Monitoring configurations
    ├── prometheus.yml
    └── grafana/
```

## 🚀 Quick Deploy

### Option 1: Docker Compose (Recommended)

```bash
# 1. Clone repository
git clone https://github.com/BlagoyKozarev/First-make.git
cd First-make/deployment

# 2. Configure environment
cp .env.example .env
nano .env  # Edit with your values

# 3. Run deployment script
sudo ./deploy.sh production

# 4. Verify
docker-compose -f docker-compose.prod.yml ps
curl http://localhost:5000/healthz
```

### Option 2: Systemd Services

```bash
# 1. Setup project directory
sudo mkdir -p /var/www/firstmake
sudo useradd -r -s /bin/false firstmake
sudo chown firstmake:firstmake /var/www/firstmake

# 2. Copy service files
sudo cp systemd/*.service /etc/systemd/system/

# 3. Configure environment
sudo cp .env.example /var/www/firstmake/.env
sudo nano /var/www/firstmake/.env

# 4. Enable and start services
sudo systemctl daemon-reload
sudo systemctl enable firstmake-api firstmake-aigateway
sudo systemctl start firstmake-api firstmake-aigateway

# 5. Check status
sudo systemctl status firstmake-api
sudo systemctl status firstmake-aigateway
```

## ⚙️ Configuration

### Environment Variables

Copy `.env.example` to `.env` and configure:

**Required:**
- `BGGPT_API_KEY` - BG GPT API key
- `ConnectionStrings__DefaultConnection` - Database path

**Optional:**
- `GRAFANA_PASSWORD` - Grafana admin password
- `DOMAIN_NAME` - Domain for SSL
- `LOG_LEVEL` - Logging verbosity

### SSL/TLS Setup

For HTTPS support:

```bash
# 1. Obtain SSL certificate (Let's Encrypt)
sudo certbot certonly --standalone -d firstmake.example.com

# 2. Copy certificates
sudo cp /etc/letsencrypt/live/firstmake.example.com/fullchain.pem deployment/nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/firstmake.example.com/privkey.pem deployment/nginx/ssl/key.pem

# 3. Update nginx config
# Edit deployment/nginx/conf.d/default.conf to enable HTTPS
```

## 📊 Monitoring

Enable monitoring stack (Prometheus + Grafana):

```bash
# Start with monitoring profile
docker-compose -f docker-compose.prod.yml --profile monitoring up -d

# Access dashboards
# Prometheus: http://localhost:9090
# Grafana:    http://localhost:3000 (admin/admin or configured password)
```

### Grafana Dashboards

Pre-configured dashboards:
- API Performance
- Request Rates
- Error Rates
- Resource Usage

## 🔄 Updates

### Update to Latest Version

```bash
# Using deployment script
sudo ./deploy.sh production

# Manual update
docker-compose -f docker-compose.prod.yml pull
docker-compose -f docker-compose.prod.yml up -d
```

### Rollback

```bash
# List available backups
ls -la /var/backups/firstmake/

# Restore from backup
sudo cp -r /var/backups/firstmake/backup_TIMESTAMP/data /var/www/firstmake/

# Restart services
docker-compose -f docker-compose.prod.yml restart
```

## 💾 Backup & Recovery

### Automatic Backups

The deployment script creates automatic backups before each deployment.

**Backup location:** `/var/backups/firstmake/backup_TIMESTAMP/`

**Retention:** Last 10 backups are kept

### Manual Backup

```bash
#!/bin/bash
BACKUP_DIR="/var/backups/firstmake/manual_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup database
cp -r /var/www/firstmake/data "$BACKUP_DIR/"

# Backup configuration
cp /var/www/firstmake/.env "$BACKUP_DIR/"

# Compress
tar -czf "$BACKUP_DIR.tar.gz" "$BACKUP_DIR"
rm -rf "$BACKUP_DIR"

echo "Backup created: $BACKUP_DIR.tar.gz"
```

### Recovery

```bash
# 1. Extract backup
tar -xzf backup_TIMESTAMP.tar.gz

# 2. Stop services
docker-compose -f docker-compose.prod.yml down

# 3. Restore data
sudo cp -r backup_TIMESTAMP/data /var/www/firstmake/

# 4. Restore configuration
sudo cp backup_TIMESTAMP/.env /var/www/firstmake/

# 5. Start services
docker-compose -f docker-compose.prod.yml up -d
```

## 🩺 Health Checks

### Service Health

```bash
# API
curl http://localhost:5000/healthz

# AI Gateway
curl http://localhost:5001/healthz

# UI
curl http://localhost:80/nginx-health
```

### Container Status

```bash
# View running containers
docker-compose -f docker-compose.prod.yml ps

# View logs
docker-compose -f docker-compose.prod.yml logs -f

# View specific service logs
docker-compose -f docker-compose.prod.yml logs -f api
```

## 🔍 Troubleshooting

### Service Won't Start

```bash
# Check logs
docker-compose -f docker-compose.prod.yml logs api

# Check environment
cat .env | grep -v "^#"

# Verify network
docker network ls
docker network inspect firstmake-network
```

### Database Issues

```bash
# Check database file
ls -lh /var/www/firstmake/data/firstmake.db

# Check permissions
sudo chown -R firstmake:firstmake /var/www/firstmake/data

# Reset database (WARNING: destroys data)
sudo rm /var/www/firstmake/data/firstmake.db
docker-compose -f docker-compose.prod.yml restart api
```

### High Memory Usage

```bash
# Check resource usage
docker stats

# Restart specific service
docker-compose -f docker-compose.prod.yml restart api

# Adjust resource limits in docker-compose.prod.yml
```

## 🔒 Security

### Firewall Configuration

```bash
# Allow HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Deny direct API access from outside (optional)
sudo ufw deny 5000/tcp
sudo ufw deny 5001/tcp

# Enable firewall
sudo ufw enable
```

### Docker Security

- All containers run as non-root users
- Network isolation via Docker bridge
- Volume permissions restricted
- Resource limits enforced

### Regular Security Updates

```bash
# Update system packages
sudo apt update && sudo apt upgrade -y

# Update Docker images
docker-compose -f docker-compose.prod.yml pull

# Restart with new images
docker-compose -f docker-compose.prod.yml up -d
```

## 📈 Performance Tuning

### Database Optimization

```sql
-- Run periodically
VACUUM;
ANALYZE;
```

### Nginx Caching

Edit `nginx/conf.d/default.conf`:

```nginx
location /static/ {
    expires 1y;
    add_header Cache-Control "public, immutable";
}
```

### Docker Resource Limits

Adjust in `docker-compose.prod.yml`:

```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
```

## 📞 Support

- **Documentation:** [/docs](../docs/)
- **Issues:** [GitHub Issues](https://github.com/BlagoyKozarev/First-make/issues)
- **Email:** (if applicable)

## 📝 License

MIT License - see [LICENSE](../LICENSE)

---

**Last Updated:** October 20, 2025
