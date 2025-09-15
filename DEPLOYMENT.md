# 🚀 AttechServer Deployment Guide

## 📋 Prerequisites

### VPS Requirements
- **OS**: Ubuntu 20.04+ hoặc CentOS 8+
- **RAM**: Tối thiểu 4GB (khuyến nghị 8GB)
- **Storage**: 50GB+
- **CPU**: 2 cores+

### Required Software on VPS
```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Install Nginx (for reverse proxy)
sudo apt install nginx -y

# Install Certbot (for SSL)
sudo apt install certbot python3-certbot-nginx -y
```

## 🐳 Quick Deploy

### Option 1: Automated Script (Recommended)
```bash
# Windows
deploy.bat production

# Linux/macOS
chmod +x deploy.sh
./deploy.sh production
```

### Option 2: Manual Deploy

#### Step 1: Upload Source Code
```bash
# Create project directory
mkdir -p /var/www/attechserver
cd /var/www/attechserver

# Upload your source code here
# (scp, git clone, or file transfer)
```

#### Step 2: Configure Environment
```bash
# Set production environment
export ASPNETCORE_ENVIRONMENT=Production

# Create necessary directories
mkdir -p uploads logs nginx/ssl
```

#### Step 3: Start Services
```bash
# Build and start all containers
docker-compose up -d --build

# Check status
docker-compose ps

# View logs
docker-compose logs -f
```

## 🔧 Configuration Details

### Environment Variables
| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment mode | `Production` |
| `SA_PASSWORD` | SQL Server password | `AttechServer@123` |
| `JWT_KEY` | JWT signing key | From appsettings |

### Port Mapping
| Service | Internal Port | External Port | Description |
|---------|---------------|---------------|-------------|
| Backend API | 80 | 5232 | Main API |
| SQL Server | 1433 | 1433 | Database |
| Nginx | 80/443 | 80/443 | Reverse Proxy |

### Volume Mounts
```yaml
volumes:
  - ./uploads:/app/Uploads        # File uploads
  - ./logs:/app/Logs             # Application logs
  - sqlserver_data:/var/opt/mssql # Database data
```

## 🔒 SSL Configuration

### Domain Setup
1. **Point your domain to VPS IP**:
   ```
   A     attech.space      103.121.89.11
   A     www.attech.space  103.121.89.11
   ```

2. **Generate SSL Certificate**:
   ```bash
   # Let's Encrypt free SSL
   sudo certbot --nginx -d attech.space -d www.attech.space

   # Auto-renewal
   sudo crontab -e
   # Add: 0 3 * * * certbot renew --quiet
   ```

### Manual SSL Setup
```bash
# Create certificate
certbot certonly --webroot \
  --webroot-path=/var/www/certbot \
  --email your-email@domain.com \
  --agree-tos \
  --no-eff-email \
  -d attech.space \
  -d www.attech.space

# Copy to nginx directory
cp /etc/letsencrypt/live/attech.space/fullchain.pem /var/www/attechserver/nginx/ssl/
cp /etc/letsencrypt/live/attech.space/privkey.pem /var/www/attechserver/nginx/ssl/

# Restart nginx
docker-compose restart nginx
```

## 🏥 Health Checks

### Endpoints to Test
```bash
# Basic health
curl https://attech.space/health

# API status
curl https://attech.space/api/health

# Database connectivity
curl https://attech.space/api/system/status
```

### Container Health
```bash
# Check all containers
docker-compose ps

# Check specific container logs
docker-compose logs backend
docker-compose logs sqlserver
docker-compose logs nginx

# Check resource usage
docker stats
```

## 🔍 Troubleshooting

### Common Issues

#### 1. Backend Won't Start
```bash
# Check logs
docker-compose logs backend

# Common causes:
# - Database connection failed
# - Port already in use
# - Configuration error
```

#### 2. Database Connection Failed
```bash
# Check SQL Server
docker-compose logs sqlserver

# Test connection
docker exec -it attechserver-db /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'AttechServer@123'
```

#### 3. SSL Certificate Issues
```bash
# Check certificate validity
openssl x509 -in /var/www/attechserver/nginx/ssl/fullchain.pem -text -noout

# Renew certificate
certbot renew --force-renewal
```

#### 4. High Memory Usage
```bash
# Check Docker stats
docker stats

# Restart services
docker-compose restart

# Clean up unused images
docker system prune -a
```

### Log Locations
- **Application**: `/var/www/attechserver/logs/`
- **Nginx**: `/var/log/nginx/`
- **Docker**: `docker-compose logs [service]`

## 🔄 Updates & Maintenance

### Update Application
```bash
# Pull latest code
git pull origin main

# Rebuild and restart
docker-compose down
docker-compose up -d --build

# Clean old images
docker image prune -a
```

### Database Backup
```bash
# Create backup
docker exec attechserver-db /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'AttechServer@123' \
  -Q "BACKUP DATABASE AttechServerDb TO DISK = '/var/opt/mssql/backup/attechserver_$(date +%Y%m%d).bak'"

# Copy backup to host
docker cp attechserver-db:/var/opt/mssql/backup/ ./backups/
```

### Monitoring Setup
```bash
# Install monitoring tools
docker run -d \
  --name monitoring \
  --restart unless-stopped \
  -p 3001:3000 \
  grafana/grafana

# Add to docker-compose for permanent setup
```

## 🌐 Production Checklist

- [ ] ✅ Domain pointing to VPS IP
- [ ] ✅ SSL certificate installed
- [ ] ✅ Firewall configured (ports 80, 443, 22)
- [ ] ✅ Database backups scheduled
- [ ] ✅ Log rotation configured
- [ ] ✅ Monitoring setup
- [ ] ✅ Security updates enabled

## 📞 Support

For deployment issues:
- **Email**: thanhnk@attech.com.vn
- **Phone**: 024.38271914
- **GitHub**: [Project Repository]

---

🎉 **Your AttechServer is now live at: https://attech.space**