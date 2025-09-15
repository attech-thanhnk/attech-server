#!/bin/bash

# AttechServer Deployment Script for VPS
# Usage: ./deploy.sh [production|staging|development]

set -e  # Exit on any error

# Configuration
ENVIRONMENT=${1:-production}
PROJECT_NAME="attechserver"
VPS_IP="103.121.89.11"
VPS_USER="root"
VPS_PATH="/var/www/attechserver"
DOMAIN="attech.space"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Starting AttechServer deployment to ${ENVIRONMENT}...${NC}"

# Step 1: Build and test locally
echo -e "${YELLOW}📦 Building project locally...${NC}"
dotnet build --configuration Release
if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Build successful!${NC}"

# Step 2: Create deployment package
echo -e "${YELLOW}📦 Creating deployment package...${NC}"
DEPLOY_PACKAGE="attechserver-$(date +%Y%m%d-%H%M%S).tar.gz"

# Include all necessary files
tar -czf $DEPLOY_PACKAGE \
    --exclude='bin' \
    --exclude='obj' \
    --exclude='.git' \
    --exclude='node_modules' \
    --exclude='*.log' \
    --exclude='Uploads/*' \
    .

echo -e "${GREEN}✅ Package created: $DEPLOY_PACKAGE${NC}"

# Step 3: Upload to VPS
echo -e "${YELLOW}📤 Uploading to VPS...${NC}"
scp $DEPLOY_PACKAGE $VPS_USER@$VPS_IP:/tmp/

# Step 4: Deploy on VPS
echo -e "${YELLOW}🚀 Deploying on VPS...${NC}"
ssh $VPS_USER@$VPS_IP << EOF
    set -e

    echo "📊 Checking available memory..."
    free -h

    echo "💾 Setting up swap if needed..."
    if [ ! -f /swapfile ]; then
        echo "Creating 2GB swap file for 3GB VPS..."
        fallocate -l 2G /swapfile
        chmod 600 /swapfile
        mkswap /swapfile
        swapon /swapfile
        echo '/swapfile none swap sw 0 0' >> /etc/fstab
        echo 'vm.swappiness=10' >> /etc/sysctl.conf
        sysctl vm.swappiness=10
        echo "✅ Swap configured!"
    fi

    echo "📁 Setting up directories..."
    mkdir -p $VPS_PATH
    mkdir -p $VPS_PATH/uploads
    mkdir -p $VPS_PATH/logs
    mkdir -p $VPS_PATH/nginx/ssl

    echo "📦 Extracting package..."
    cd $VPS_PATH
    tar -xzf /tmp/$DEPLOY_PACKAGE

    echo "🔧 Setting permissions..."
    chown -R www-data:www-data $VPS_PATH/uploads
    chown -R www-data:www-data $VPS_PATH/logs
    chmod -R 755 $VPS_PATH

    echo "🧹 Cleaning old Docker images to save space..."
    docker system prune -f || true

    echo "🐳 Stopping existing containers..."
    docker-compose down || true

    echo "🏗️ Building containers with memory optimization..."
    # Build with limited resources to avoid OOM
    docker-compose build --no-cache --parallel

    echo "🚀 Starting containers sequentially to avoid memory spikes..."
    # Start SQL Server first and wait
    docker-compose up -d sqlserver
    echo "⏳ Waiting for SQL Server to start (60s)..."
    sleep 60

    # Start backend
    docker-compose up -d backend
    echo "⏳ Waiting for backend to start (45s)..."
    sleep 45

    # Start nginx
    docker-compose up -d nginx
    echo "⏳ Waiting for nginx to start (15s)..."
    sleep 15

    echo "📊 Memory usage after startup:"
    free -h

    echo "🏥 Checking health..."
    docker-compose ps

    # Extended health check for low-resource VPS
    echo "⏳ Waiting for services to stabilize..."
    sleep 30

    # Check if backend is responding
    MAX_RETRIES=5
    RETRY_COUNT=0
    while [ \$RETRY_COUNT -lt \$MAX_RETRIES ]; do
        if curl -f http://localhost:5232/health > /dev/null 2>&1; then
            echo "✅ Backend is healthy!"
            break
        else
            echo "⏳ Backend not ready yet, retrying (\$((RETRY_COUNT + 1))/\$MAX_RETRIES)..."
            sleep 10
            RETRY_COUNT=\$((RETRY_COUNT + 1))
        fi
    done

    if [ \$RETRY_COUNT -eq \$MAX_RETRIES ]; then
        echo "❌ Backend health check failed after \$MAX_RETRIES attempts!"
        echo "🔍 Checking logs..."
        docker-compose logs --tail=50 backend
        echo "📊 Memory status:"
        free -h
        exit 1
    fi

    echo "🧹 Cleaning up..."
    rm -f /tmp/$DEPLOY_PACKAGE
    docker system prune -f

    echo "✅ Deployment completed successfully on 3GB VPS!"
    echo "📊 Final memory status:"
    free -h
EOF

# Step 5: Setup SSL (if not already done)
echo -e "${YELLOW}🔒 Setting up SSL certificate...${NC}"
ssh $VPS_USER@$VPS_IP << 'EOF'
    # Install certbot if not present
    if ! command -v certbot &> /dev/null; then
        echo "📦 Installing certbot..."
        apt-get update
        apt-get install -y certbot python3-certbot-nginx
    fi

    # Create SSL certificate if it doesn't exist
    if [ ! -f /var/www/attechserver/nginx/ssl/fullchain.pem ]; then
        echo "🔐 Creating SSL certificate..."
        certbot certonly \
            --webroot \
            --webroot-path=/var/www/certbot \
            --email thanhnk@attech.com.vn \
            --agree-tos \
            --no-eff-email \
            -d attech.space \
            -d www.attech.space

        # Copy certificates to nginx directory
        cp /etc/letsencrypt/live/attech.space/fullchain.pem /var/www/attechserver/nginx/ssl/
        cp /etc/letsencrypt/live/attech.space/privkey.pem /var/www/attechserver/nginx/ssl/

        # Set permissions
        chmod 644 /var/www/attechserver/nginx/ssl/fullchain.pem
        chmod 600 /var/www/attechserver/nginx/ssl/privkey.pem

        # Restart nginx
        docker-compose restart nginx
    fi

    # Setup auto-renewal
    if ! crontab -l | grep -q certbot; then
        echo "⏰ Setting up certificate auto-renewal..."
        (crontab -l 2>/dev/null; echo "0 3 * * * certbot renew --quiet && docker-compose restart nginx") | crontab -
    fi
EOF

# Step 6: Final verification
echo -e "${YELLOW}🔍 Final verification...${NC}"
echo -e "${BLUE}Testing endpoints:${NC}"

# Test HTTP (should redirect to HTTPS)
HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://$DOMAIN/health || echo "000")
echo "HTTP /health: $HTTP_STATUS"

# Test HTTPS
HTTPS_STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://$DOMAIN/health || echo "000")
echo "HTTPS /health: $HTTPS_STATUS"

if [ "$HTTPS_STATUS" = "200" ]; then
    echo -e "${GREEN}🎉 Deployment successful!${NC}"
    echo -e "${GREEN}🌐 Your API is live at: https://$DOMAIN${NC}"
    echo -e "${GREEN}📖 API Documentation: https://$DOMAIN/swagger${NC}"
    echo -e "${GREEN}🏥 Health Check: https://$DOMAIN/health${NC}"
else
    echo -e "${RED}❌ Deployment verification failed!${NC}"
    echo -e "${YELLOW}Check the logs on the server:${NC}"
    echo "ssh $VPS_USER@$VPS_IP 'cd $VPS_PATH && docker-compose logs'"
fi

# Cleanup local package
rm -f $DEPLOY_PACKAGE

echo -e "${BLUE}🏁 Deployment script completed!${NC}"