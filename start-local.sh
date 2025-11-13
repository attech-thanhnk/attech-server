#!/bin/bash

################################################################################
# Attech Server - Local Development Fullstack Startup
################################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env.local"

echo "========================================"
echo " ATTECH SERVER - LOCAL DEVELOPMENT"
echo "========================================"
echo ""

# Check .env.local
if [ ! -f "$ENV_FILE" ]; then
    echo "[WARNING] .env.local not found!"
    echo "Creating from example..."
    cp .env.local.example .env.local
    echo ""
    echo "[ACTION REQUIRED] Edit .env.local and update FRONTEND_SOURCE_DIR"
    echo "Then run this script again."
    exit 1
fi

# Load environment
echo "[1/5] Loading configuration..."
set -a
source "$ENV_FILE"
set +a
echo "  ✓ Configuration loaded"

# Check Docker
if ! docker info > /dev/null 2>&1; then
    echo "[ERROR] Docker is not running!"
    echo "Please start Docker Desktop and try again."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "[ERROR] docker-compose is not installed!"
    exit 1
fi

echo ""
echo "[2/5] Checking frontend source directory..."
if [ ! -d "$FRONTEND_SOURCE_DIR" ]; then
    echo "[ERROR] Frontend source directory not found: $FRONTEND_SOURCE_DIR"
    echo "Please update FRONTEND_SOURCE_DIR in .env.local"
    exit 1
fi
echo "  ✓ Frontend source found at: $FRONTEND_SOURCE_DIR"

echo ""
echo "[3/5] Creating necessary directories..."
mkdir -p uploads logs

echo ""
echo "[4/5] Building and starting containers..."
docker-compose -f docker-compose.fullstack.yml -f docker-compose.local.yml --env-file .env.local up -d --build

echo ""
echo "[5/5] Waiting for services..."
sleep 10

echo ""
echo "========================================"
echo " DEPLOYMENT COMPLETED!"
echo "========================================"
echo ""
echo "  Frontend:  http://localhost"
echo "  Backend:   http://localhost/api"
echo ""
echo "View logs:"
echo "  docker-compose -f docker-compose.fullstack.yml -f docker-compose.local.yml logs -f"
echo ""
echo "Stop services:"
echo "  docker-compose -f docker-compose.fullstack.yml -f docker-compose.local.yml down"
echo ""

docker-compose -f docker-compose.fullstack.yml ps
