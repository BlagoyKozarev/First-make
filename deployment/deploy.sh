#!/bin/bash
# Production Deployment Script for FirstMake Agent
# Usage: ./deploy.sh [environment]

set -e

# Configuration
ENV=${1:-production}
PROJECT_DIR="/var/www/firstmake"
BACKUP_DIR="/var/backups/firstmake"
LOG_FILE="/var/log/firstmake/deployment.log"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" | tee -a "$LOG_FILE"
    exit 1
}

warn() {
    echo -e "${YELLOW}[WARNING]${NC} $1" | tee -a "$LOG_FILE"
}

# Check if running as root or with sudo
if [[ $EUID -ne 0 ]]; then
   error "This script must be run as root or with sudo"
fi

log "Starting FirstMake Agent deployment to $ENV environment"

# Step 1: Pre-deployment checks
log "Step 1: Running pre-deployment checks..."

# Check Docker
if ! command -v docker &> /dev/null; then
    error "Docker is not installed"
fi

# Check Docker Compose
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
    error "Docker Compose is not installed"
fi

# Check disk space (require at least 5GB free)
AVAILABLE_SPACE=$(df -BG / | awk 'NR==2 {print $4}' | sed 's/G//')
if [ "$AVAILABLE_SPACE" -lt 5 ]; then
    error "Insufficient disk space. At least 5GB required, only ${AVAILABLE_SPACE}GB available"
fi

log "✓ Pre-deployment checks passed"

# Step 2: Create backup
log "Step 2: Creating backup..."

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_PATH="$BACKUP_DIR/backup_$TIMESTAMP"

mkdir -p "$BACKUP_DIR"

if [ -d "$PROJECT_DIR/data" ]; then
    log "Backing up database..."
    cp -r "$PROJECT_DIR/data" "$BACKUP_PATH/data"
fi

if [ -f "$PROJECT_DIR/.env" ]; then
    log "Backing up configuration..."
    cp "$PROJECT_DIR/.env" "$BACKUP_PATH/.env"
fi

# Keep only last 10 backups
log "Cleaning old backups..."
ls -t "$BACKUP_DIR" | tail -n +11 | xargs -I {} rm -rf "$BACKUP_DIR/{}"

log "✓ Backup created at $BACKUP_PATH"

# Step 3: Pull latest images
log "Step 3: Pulling latest Docker images..."

cd "$PROJECT_DIR"

if [ -f "deployment/docker-compose.prod.yml" ]; then
    docker-compose -f deployment/docker-compose.prod.yml pull || error "Failed to pull images"
else
    error "docker-compose.prod.yml not found"
fi

log "✓ Images pulled successfully"

# Step 4: Stop running services
log "Step 4: Stopping running services..."

docker-compose -f deployment/docker-compose.prod.yml down --remove-orphans || warn "No running services to stop"

log "✓ Services stopped"

# Step 5: Database migrations
log "Step 5: Running database migrations..."

# Check if database exists
if [ -f "$PROJECT_DIR/data/firstmake.db" ]; then
    log "Database exists, running migrations..."
    
    # Run migrations using a temporary container
    docker run --rm \
        -v "$PROJECT_DIR/data:/data" \
        ghcr.io/blagoyko zarev/first-make-api:latest \
        dotnet ef database update || warn "Migration failed or not needed"
else
    log "No existing database, will be created on first run"
fi

log "✓ Database migrations completed"

# Step 6: Start services
log "Step 6: Starting services..."

docker-compose -f deployment/docker-compose.prod.yml up -d || error "Failed to start services"

log "✓ Services started"

# Step 7: Health checks
log "Step 7: Running health checks..."

# Wait for services to start
sleep 10

# Check API health
log "Checking API health..."
for i in {1..30}; do
    if curl -sf http://localhost:5000/healthz > /dev/null 2>&1; then
        log "✓ API is healthy"
        break
    fi
    if [ $i -eq 30 ]; then
        error "API health check failed"
    fi
    sleep 2
done

# Check AI Gateway health
log "Checking AI Gateway health..."
for i in {1..30}; do
    if curl -sf http://localhost:5001/healthz > /dev/null 2>&1; then
        log "✓ AI Gateway is healthy"
        break
    fi
    if [ $i -eq 30 ]; then
        error "AI Gateway health check failed"
    fi
    sleep 2
done

# Check UI
log "Checking UI..."
for i in {1..30}; do
    if curl -sf http://localhost:80/nginx-health > /dev/null 2>&1; then
        log "✓ UI is healthy"
        break
    fi
    if [ $i -eq 30 ]; then
        error "UI health check failed"
    fi
    sleep 2
done

log "✓ All health checks passed"

# Step 8: Display status
log "Step 8: Deployment status"

echo ""
echo "====== Deployment Complete ======"
docker-compose -f deployment/docker-compose.prod.yml ps
echo ""

log "FirstMake Agent successfully deployed to $ENV environment"
log "Services are accessible at:"
log "  - UI:         http://localhost (or configured domain)"
log "  - API:        http://localhost:5000"
log "  - AI Gateway: http://localhost:5001"
log ""
log "To view logs: docker-compose -f deployment/docker-compose.prod.yml logs -f"
log "To stop:      docker-compose -f deployment/docker-compose.prod.yml down"
log ""
log "Backup location: $BACKUP_PATH"

exit 0
