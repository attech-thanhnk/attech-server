# AttechServer

Backend .NET 9 + Frontend React + SQL Server 2022 với CI/CD tự động

---

## 🚀 Quick Start

### Local Development
```bash
# Build từ source
./start-local.sh
```
→ http://localhost/

### Production (VPS)
Chi tiết xem [SETUP.md](SETUP.md)

---

## 🤖 CI/CD Workflow

**Kiến trúc:**
```
Backend repo → Push → GitHub Actions → Build image → Deploy
Frontend repo → Push → GitHub Actions → Build image → Deploy
```

**Mỗi khi push code:**
- Backend: Tự động test + build + deploy
- Frontend: Tự động build + deploy
- Không cần chạy script thủ công

---

## 📁 Cấu Trúc

```
AttechServer/
├── .github/workflows/deploy-backend.yml  # CI/CD
├── docker-compose.fullstack.yml          # Production (dùng images)
├── docker-compose.local.yml              # Local dev (build từ source)
├── initial-setup.sh                      # Chỉ chạy 1 lần khi setup VPS
├── start-local.sh                        # Local development
├── .env.production.example               # Production config
├── .env.local.example                    # Local config
└── nginx/proxy/conf.d/
    ├── local.conf                        # Development routing
    └── production.conf.template          # Production template
```

---

## 📦 Container Images

**Lưu trữ tại GitHub Container Registry:**
- Backend: `ghcr.io/attech-thanhnk/attech-server:latest`
- Frontend: `ghcr.io/attech-thanhnk/attech-client:latest`

**Tự động build khi push code**

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

### Production
```bash
# Xem logs
docker logs attechserver-api -f
docker logs attechserver-frontend -f

# Restart service (CI/CD sẽ tự động, nhưng nếu cần manual)
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml restart backend

# Stop all
docker-compose -f docker-compose.fullstack.yml -f docker-compose.fullstack.production.yml down
```

### Local
```bash
# Xem logs
docker-compose -f docker-compose.fullstack.yml -f docker-compose.local.yml logs -f

# Stop
docker-compose -f docker-compose.fullstack.yml -f docker-compose.local.yml down
```

---

## 🏗️ Architecture

```
Nginx Proxy (Port 80, 443)
    ├── Frontend (React container)
    └── Backend (.NET 9 container) → SQL Server
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
| CI/CD failed | Kiểm tra GitHub Secrets (VPS_HOST, VPS_SSH_KEY, etc) |
| Image pull failed | Kiểm tra image có tồn tại tại ghcr.io |
| Port 80 busy | `sudo systemctl stop nginx` hoặc `net stop http` |

---

## 📚 Documentation

- **[SETUP.md](SETUP.md)** - Hướng dẫn setup VPS từ đầu (9 bước chi tiết)

---

✅ **Production Ready** | CI/CD Enabled | v2.0.0
