# Setup Production VPS - Checklist

## ✅ Các Bước Thực Hiện (Theo Thứ Tự)

### Bước 1: Chuẩn Bị GitHub Secrets

**Backend Repo:** https://github.com/attech-thanhnk/attech-server/settings/secrets/actions

Tạo 4 secrets:
```
VPS_HOST = <IP hoặc domain VPS của bạn>
VPS_USERNAME = thanhdev
VPS_SSH_KEY = <SSH private key - xem bước 3>
VPS_PORT = 22
```

**Frontend Repo:** https://github.com/attech-thanhnk/attech-client/settings/secrets/actions

Tạo 4 secrets:
```
SERVER_HOST = <IP hoặc domain VPS của bạn>
SERVER_USER = thanhdev
SSH_PRIVATE_KEY = <SSH private key - xem bước 3>
SERVER_PORT = 22
```

### Bước 2: SSH vào VPS

```bash
ssh thanhdev@your-vps-ip
```

### Bước 3: Tạo SSH Key cho GitHub Actions

```bash
# Tạo key
ssh-keygen -t ed25519 -C "github-actions" -f ~/.ssh/github_actions

# Thêm vào authorized_keys
cat ~/.ssh/github_actions.pub >> ~/.ssh/authorized_keys

# Set permissions
chmod 600 ~/.ssh/github_actions
chmod 600 ~/.ssh/authorized_keys

# COPY private key này và paste vào GitHub Secrets
cat ~/.ssh/github_actions
```

**→ Copy toàn bộ output (bao gồm `-----BEGIN` và `-----END`)** paste vào:
- Backend: `VPS_SSH_KEY`
- Frontend: `SSH_PRIVATE_KEY`

### Bước 4: Clone Backend Repo

```bash
cd /home/thanhdev
git clone https://github.com/attech-thanhnk/attech-server.git AttechServer
cd AttechServer
```

### Bước 5: Tạo .env.production

```bash
cp .env.production.example .env.production
nano .env.production
```

**Sửa các giá trị sau:**
```bash
# Domains (thay thế bằng domain thật của bạn)
FRONTEND_DOMAIN=attech.space
FRONTEND_DOMAIN_WWW=www.attech.space
API_DOMAIN=api.attech.space

# Database password (đổi password mạnh hơn)
SA_PASSWORD=YourStrongPassword@123

# SMTP (nếu cần gửi email)
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your-app-password
```

Lưu lại: `Ctrl+O`, `Enter`, `Ctrl+X`

### Bước 6: Setup SSL Certificate

```bash
# Cài certbot (nếu chưa có)
sudo apt update
sudo apt install certbot -y

# Dừng services đang chạy trên port 80/443
sudo systemctl stop nginx || true
docker stop $(docker ps -q) || true

# Tạo SSL certificate
sudo certbot certonly --standalone \
  -d attech.space \
  -d www.attech.space \
  -d api.attech.space \
  --email your-email@gmail.com \
  --agree-tos \
  --non-interactive

# Verify certificate đã được tạo
sudo ls -la /etc/letsencrypt/live/api.attech.space/
```

**Expected:** Thấy các file `fullchain.pem`, `privkey.pem`

### Bước 7: Chạy Initial Setup

```bash
cd /home/thanhdev/AttechServer
chmod +x initial-setup.sh
sudo ./initial-setup.sh
```

**Script sẽ:**
- Pull latest images (backend + frontend)
- Tạo nginx config
- Start tất cả containers
- Run health checks

### Bước 8: Verify Deployment

```bash
# Xem containers
docker ps

# Test endpoints
curl https://api.attech.space/health
curl https://attech.space

# Xem logs nếu cần
docker logs attechserver-api
docker logs attechserver-frontend
```

### Bước 9: Test CI/CD

**Test Backend CI/CD:**
```bash
# Trên máy local
cd C:\Users\Admin\source\repos\AttechServer
echo "# Test" >> README.md
git add .
git commit -m "test: Backend CI/CD"
git push origin main
```

→ Vào GitHub Actions: https://github.com/attech-thanhnk/attech-server/actions
→ Xem workflow "Backend CI/CD" chạy
→ Sau khi xong, check `curl https://api.attech.space/health`

**Test Frontend CI/CD:**
```bash
# Trên máy local
cd C:\Users\Admin\ThanhNK\attech-client
echo "# Test" >> README.md
git add .
git commit -m "test: Frontend CI/CD"
git push origin main
```

→ Vào GitHub Actions: https://github.com/attech-thanhnk/attech-client/actions
→ Xem workflow chạy
→ Sau khi xong, check `curl https://attech.space`

## ✅ Hoàn Thành!

**Từ giờ:**
- Sửa backend → push → tự động deploy
- Sửa frontend → push → tự động deploy

## 🔧 Troubleshooting

### Lỗi: Permission denied (publickey)
```bash
# Trên VPS, kiểm tra lại authorized_keys
cat ~/.ssh/authorized_keys | grep github-actions
```

### Lỗi: Docker image pull failed
```bash
# Kiểm tra login registry
echo "YOUR_GITHUB_TOKEN" | docker login ghcr.io -u USERNAME --password-stdin

# Pull manual
docker pull ghcr.io/attech-thanhnk/attech-server:latest
docker pull ghcr.io/attech-thanhnk/attech-client:latest
```

### Lỗi: SSL certificate not found
```bash
# Tạo lại certificate
sudo certbot certonly --standalone -d attech.space -d www.attech.space -d api.attech.space
```

### Lỗi: Port 80/443 already in use
```bash
# Tìm process đang dùng
sudo lsof -i :80
sudo lsof -i :443

# Stop nginx/apache nếu có
sudo systemctl stop nginx
sudo systemctl stop apache2
```

## 📝 Lưu Ý Quan Trọng

1. **Domain phải trỏ về VPS trước khi chạy certbot**
2. **GitHub secrets phải chính xác (không có space thừa)**
3. **SSH key phải include cả BEGIN/END lines**
4. **Lần đầu chạy `initial-setup.sh`, sau đó CI/CD tự động**
