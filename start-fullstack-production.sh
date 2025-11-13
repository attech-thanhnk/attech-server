#!/bin/bash

################################################################################
# Attech Server - Fullstack Production Deployment
################################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env.production"

echo "========================================"
echo " ATTECH SERVER - PRODUCTION DEPLOYMENT"
echo "========================================"
echo ""

# Check root
if [ "$EUID" -ne 0 ]; then
    echo "[ERROR] Please run as root (use sudo)"
    exit 1
fi

# Check .env.production
if [ ! -f "$ENV_FILE" ]; then
    echo "[ERROR] .env.production not found!"
    echo "Create it from example and configure your domains."
    exit 1
fi

# Load environment
echo "[1/9] Loading configuration..."
set -a
source "$ENV_FILE"
set +a
echo "  ✓ Configuration loaded"

# Check Docker
if ! docker info > /dev/null 2>&1; then
    echo "[ERROR] Docker is not running!"
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "[ERROR] docker-compose is not installed!"
    exit 1
fi

echo ""
echo "[2/9] Generating nginx production config..."
# Generate nginx config inline
if command -v envsubst &> /dev/null; then
    envsubst < "${SCRIPT_DIR}/nginx/proxy/conf.d/production.conf.template" > "${SCRIPT_DIR}/nginx/proxy/conf.d/production.conf"
    echo "  ✓ Generated nginx config"
else
    echo "[ERROR] envsubst not found! Install: sudo apt install gettext-base"
    exit 1
fi

echo ""
echo "[3/9] Generating docker-compose production config..."
cat > "${SCRIPT_DIR}/docker-compose.fullstack.production.yml" << EOF
# Production overrides - Auto-generated
services:
  backend:
    environment:
      - ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
      - ConnectionStrings__Default=Data Source=sqlserver,1433;Initial Catalog=${DB_NAME:-AttechServerDb};User ID=sa;Password=${SA_PASSWORD};Trust Server Certificate=True;
    mem_limit: ${BACKEND_MEM_LIMIT:-1000}m
    mem_reservation: ${BACKEND_MEM_RESERVATION:-600}m
    volumes:
      - ${UPLOADS_DIR}:/app/Uploads
      - ./logs:/app/Logs

  frontend:
    build:
      context: ${FRONTEND_SOURCE_DIR}
      dockerfile: Dockerfile
      args:
        REACT_APP_API_PROTOCOL: https
        REACT_APP_API_HOST: ${API_DOMAIN}
        REACT_APP_API_PORT: ""
    mem_limit: ${FRONTEND_MEM_LIMIT:-150}m
    mem_reservation: ${FRONTEND_MEM_RESERVATION:-100}m

  sqlserver:
    environment:
      SA_PASSWORD: ${SA_PASSWORD}
      MSSQL_MEMORY_LIMIT_MB: ${SQLSERVER_MEMORY_LIMIT_MB:-1024}
    mem_limit: ${SQLSERVER_MEM_LIMIT:-1200}m
    mem_reservation: ${SQLSERVER_MEM_RESERVATION:-800}m

  proxy:
    volumes:
      - ./nginx/proxy/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/proxy/conf.d/production.conf:/etc/nginx/conf.d/default.conf:ro
      - ${SSL_CERT_BASE}/${API_DOMAIN}:/etc/nginx/ssl:ro
      - ${SSL_OPTIONS_FILE}:/etc/letsencrypt/options-ssl-nginx.conf:ro
      - ${SSL_DHPARAMS_FILE}:/etc/letsencrypt/ssl-dhparams.pem:ro
      - ${UPLOADS_DIR}:/var/www/uploads:ro
      - certbot_www:/var/www/certbot:ro

volumes:
  certbot_www:
    name: certbot_www
EOF
echo "  ✓ Generated docker-compose config"

echo ""
echo "[4/9] Stopping existing containers..."
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml down

echo ""
echo "[5/9] Creating necessary directories..."
mkdir -p uploads logs

echo ""
echo "[6/9] Checking SSL certificates..."
SSL_CERT_PATH="${SSL_CERT_BASE}/${API_DOMAIN}"
if [ ! -f "${SSL_CERT_PATH}/fullchain.pem" ]; then
    echo "[ERROR] SSL certificates not found at ${SSL_CERT_PATH}/"
    echo "Run: sudo certbot certonly --standalone -d ${FRONTEND_DOMAIN} -d ${FRONTEND_DOMAIN_WWW} -d ${API_DOMAIN}"
    exit 1
fi
echo "  ✓ SSL certificates found"

echo ""
echo "[7/9] Building containers..."
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml build

echo ""
echo "[8/9] Starting all services..."
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml up -d

echo ""
echo "[9/9] Waiting for services..."
sleep 15

echo ""
echo "========================================"
echo " DEPLOYMENT COMPLETED!"
echo "========================================"
echo ""
echo "  Frontend:  https://${FRONTEND_DOMAIN}"
echo "  Backend:   https://${API_DOMAIN}"
echo ""
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml ps
echo ""
