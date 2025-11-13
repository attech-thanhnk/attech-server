# AttechServer - Docker Fullstack

Backend .NET 9 + Frontend React + SQL Server 2022

---

## 🚀 Quick Start

### Development (Windows)
```bash
docker-compose -f docker-compose.fullstack.yml up -d
```
→ http://localhost/

### Production (Ubuntu)
```bash
cp .env.production.example .env.production
nano .env.production  # Sửa 3 dòng domain
sudo ./start-fullstack-production.sh
```

---

## 📁 Files Cần Thiết (5 files)

```
AttechServer/
├── docker-compose.fullstack.yml       # Config Docker
├── .env.production.example            # Template config (copy → .env.production)
├── start-fullstack-production.sh      # Deploy script (all-in-one)
└── nginx/proxy/conf.d/
    ├── local.conf                     # Development routing
    └── production.conf.template       # Production template
```

**Note:**
- `start-fullstack-production.sh` tự động generate: `production.conf` + `docker-compose.fullstack.production.yml`
- Không cần commit 2 files generated này

---

## 🎯 Deploy Lên Server Mới

**Chỉ cần sửa 3 dòng:**
```bash
FRONTEND_DOMAIN=yourdomain.com
FRONTEND_DOMAIN_WWW=www.yourdomain.com
API_DOMAIN=api.yourdomain.com
```

**Script tự động:**
✅ Generate nginx config
✅ Generate docker-compose config
✅ Build containers
✅ Deploy

---

## ⚙️ Requirements

**Development:** Docker Desktop

**Production:**
```bash
sudo apt install docker.io docker-compose gettext-base certbot

# SSL
sudo certbot certonly --standalone \
  -d domain.com -d www.domain.com -d api.domain.com
```

---

## 🌐 Endpoints

| Service | URL |
|---------|-----|
| Frontend | http://localhost/ |
| API | http://localhost/api/ |
| Swagger | http://localhost/swagger |
| Health | http://localhost/health |

---

## 🛠️ Commands

```bash
# Logs
docker-compose -f docker-compose.fullstack.yml logs -f

# Restart service
docker-compose -f docker-compose.fullstack.yml restart backend

# Stop all
docker-compose -f docker-compose.fullstack.yml down
```

---

## 🏗️ Architecture

```
Nginx Proxy (Port 80, 443)
    ├── Frontend (React)
    └── Backend (.NET 9) → SQL Server 2022
```

**Resources (3GB VPS):**
- SQL Server: 1200MB
- Backend: 1000MB
- Frontend: 150MB
- Proxy: 100MB

---

## 🔧 Troubleshooting

| Issue | Solution |
|-------|----------|
| Backend culture error | ✅ Fixed (ICU + globalization) |
| api.localhost fails (Windows) | Use `http://localhost/api/` |
| Port 80 busy | `net stop http` or `sudo systemctl stop nginx` |

---

✅ **Production Ready** | v1.0.0 | 2025-11-12
