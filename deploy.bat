@echo off
REM AttechServer Deployment Script for Windows
REM Usage: deploy.bat [production|staging|development]

setlocal enabledelayedexpansion

set ENVIRONMENT=%1
if "%ENVIRONMENT%"=="" set ENVIRONMENT=production

set PROJECT_NAME=attechserver
set VPS_IP=103.121.89.11
set VPS_USER=root
set VPS_PATH=/var/www/attechserver
set DOMAIN=attech.space

echo 🚀 Starting AttechServer deployment to %ENVIRONMENT%...

REM Step 1: Build project locally
echo 📦 Building project locally...
dotnet build --configuration Release
if errorlevel 1 (
    echo ❌ Build failed!
    exit /b 1
)
echo ✅ Build successful!

REM Step 2: Create deployment package
echo 📦 Creating deployment package...
set TIMESTAMP=%date:~10,4%%date:~4,2%%date:~7,2%-%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set DEPLOY_PACKAGE=attechserver-%TIMESTAMP%.tar.gz

REM Create tar.gz using 7zip or tar (if available)
if exist "C:\Program Files\7-Zip\7z.exe" (
    "C:\Program Files\7-Zip\7z.exe" a -tgzip %DEPLOY_PACKAGE% * -xr!bin -xr!obj -xr!.git -xr!node_modules -xr!*.log -xr!Uploads\*
) else (
    tar -czf %DEPLOY_PACKAGE% --exclude=bin --exclude=obj --exclude=.git --exclude=node_modules --exclude=*.log --exclude=Uploads/* .
)

echo ✅ Package created: %DEPLOY_PACKAGE%

REM Step 3: Upload to VPS using SCP (requires Windows 10 OpenSSH or PuTTY)
echo 📤 Uploading to VPS...
scp %DEPLOY_PACKAGE% %VPS_USER%@%VPS_IP%:/tmp/

REM Step 4: Deploy on VPS using SSH
echo 🚀 Deploying on VPS...
ssh %VPS_USER%@%VPS_IP% "cd %VPS_PATH% && tar -xzf /tmp/%DEPLOY_PACKAGE% && docker-compose down && docker-compose up -d --build"

REM Step 5: Verification
echo 🔍 Verifying deployment...
timeout /t 30 > nul

REM Test health endpoint
for /f "delims=" %%i in ('curl -s -o NUL -w "%%{http_code}" https://%DOMAIN%/health 2^>NUL') do set HEALTH_STATUS=%%i

if "%HEALTH_STATUS%"=="200" (
    echo 🎉 Deployment successful!
    echo 🌐 Your API is live at: https://%DOMAIN%
    echo 📖 API Documentation: https://%DOMAIN%/swagger
    echo 🏥 Health Check: https://%DOMAIN%/health
) else (
    echo ❌ Deployment verification failed!
    echo Health status: %HEALTH_STATUS%
)

REM Cleanup
del %DEPLOY_PACKAGE%

echo 🏁 Deployment script completed!
pause