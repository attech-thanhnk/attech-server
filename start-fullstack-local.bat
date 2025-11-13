@echo off
REM ==============================================================================
REM Attech Server - Fullstack Docker Deployment (Local Development)
REM ==============================================================================
REM This script deploys the full stack with 4 containers:
REM   1. Nginx Proxy (Entry point - port 80)
REM   2. Frontend (React)
REM   3. Backend (.NET 9 API)
REM   4. SQL Server 2022
REM ==============================================================================

echo ========================================
echo  ATTECH SERVER - FULLSTACK DEPLOYMENT
echo  Environment: LOCAL DEVELOPMENT
echo ========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Docker is not running!
    echo Please start Docker Desktop and try again.
    pause
    exit /b 1
)

echo [1/6] Stopping existing containers...
docker-compose -f docker-compose.fullstack.yml down

echo.
echo [2/6] Removing old images (optional - press Ctrl+C to skip)...
timeout /t 5 /nobreak
docker-compose -f docker-compose.fullstack.yml build --no-cache

echo.
echo [3/6] Creating necessary directories...
if not exist "uploads" mkdir uploads
if not exist "logs" mkdir logs

echo.
echo [4/6] Building containers...
docker-compose -f docker-compose.fullstack.yml build

echo.
echo [5/6] Starting all services...
docker-compose -f docker-compose.fullstack.yml up -d

echo.
echo [6/6] Waiting for services to be healthy...
timeout /t 10

echo.
echo ========================================
echo  DEPLOYMENT COMPLETED!
echo ========================================
echo.
echo  Frontend:  http://localhost
echo  Backend:   http://api.localhost
echo  Swagger:   http://api.localhost/swagger
echo.
echo  Database:  localhost:1433
echo  Username:  sa
echo  Password:  AttechServer@123
echo.
echo ========================================
echo.

REM Show container status
echo Current container status:
docker-compose -f docker-compose.fullstack.yml ps

echo.
echo Press any key to view logs (Ctrl+C to exit logs)...
pause >nul

docker-compose -f docker-compose.fullstack.yml logs -f
