# 🚀 HƯỚNG DẪN SETUP VPS PRODUCTION - ĐẦY ĐỦ

## 📋 MỤC LỤC
- [Bước 1: Yêu cầu VPS](#bước-1-yêu-cầu-vps)
- [Bước 2: Truy cập & Cập nhật hệ thống](#bước-2-truy-cập--cập-nhật-hệ-thống)
- [Bước 3: Cài đặt Docker](#bước-3-cài-đặt-docker--docker-compose)
- [Bước 4: Cài envsubst](#bước-4-cài-envsubst)
- [Bước 5: Setup Firewall](#bước-5-setup-firewall)
- [Bước 6: Tạo User](#bước-6-tạo-user-optional)
- [Bước 7: Clone code](#bước-7-clone-code-từ-github)
- [Bước 8: Cấu hình .env](#bước-8-cấu-hình-envproduction)
- [Bước 9: Cấu hình DNS](#bước-9-cấu-hình-dns)
- [Bước 10: Cài SSL](#bước-10-cài-đặt-ssl-certificate)
- [Bước 11: Deploy](#bước-11-deploy-production)
- [Bước 12: Kiểm tra](#bước-12-kiểm-tra-hệ-thống)
- [Bảo mật bổ sung](#-bảo-mật-bổ-sung)
- [Troubleshooting](#-troubleshooting)

---

## **BƯỚC 1: YÊU CẦU VPS**

### **Cấu hình khuyến nghị:**
```
CPU:       2-4 cores
RAM:       4-8 GB (tối thiểu 4GB)
Disk:      40-80 GB SSD
OS:        Ubuntu 22.04 LTS hoặc Ubuntu 24.04 LTS
Bandwidth: Unlimited hoặc ≥1TB/tháng
```

### **Nhà cung cấp gợi ý:**
- **DigitalOcean** - Droplet $24/tháng (4GB RAM)
- **Vultr** - Cloud Compute $18/tháng (4GB RAM)
- **Linode** - Shared $24/tháng (4GB RAM)
- **AWS Lightsail** - $20/tháng (4GB RAM)
- **Contabo** - Rẻ nhưng tốc độ chậm hơn

### **Domain cần chuẩn bị:**
- `attech.space` → Frontend (website chính)
- `www.attech.space` → Frontend alias (optional)
- `api.attech.space` → Backend API

---

## **BƯỚC 2: TRUY CẬP & CẬP NHẬT HỆ THỐNG**

```bash
# SSH vào VPS
ssh root@YOUR_VPS_IP

# Cập nhật hệ thống
apt update && apt upgrade -y

# Cài đặt tools cơ bản
apt install -y curl wget git vim nano ufw
```

---

## **BƯỚC 3: CÀI ĐẶT DOCKER & DOCKER COMPOSE**

```bash
# Cài đặt Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Kích hoạt Docker
systemctl enable docker
systemctl start docker

# Kiểm tra
docker --version

# Cài Docker Compose
apt install -y docker-compose

# Kiểm tra
docker-compose --version
```

---

## **BƯỚC 4: CÀI ENVSUBST**

```bash
# Cài envsubst (dùng để generate nginx config)
apt install -y gettext-base

# Kiểm tra
envsubst --version
```

---

## **BƯỚC 5: SETUP FIREWALL**

```bash
# Enable UFW firewall
ufw allow OpenSSH
ufw allow 80/tcp     # HTTP
ufw allow 443/tcp    # HTTPS
ufw enable

# Kiểm tra
ufw status
```

---

## **BƯỚC 6: TẠO USER (Optional - Khuyến nghị)**

```bash
# Tạo user mới (thay 'thanhdev' bằng tên của bạn)
adduser thanhdev

# Thêm vào sudo group
usermod -aG sudo thanhdev

# Thêm vào docker group
usermod -aG docker thanhdev

# Chuyển sang user mới
su - thanhdev
```

---

## **BƯỚC 7: CLONE CODE TỪ GITHUB**

```bash
# Tạo thư mục làm việc
mkdir -p ~/projects
cd ~/projects

# Clone backend repository
git clone https://github.com/attech-thanhnk/attech-server.git
cd attech-server

# Clone frontend repository
cd ~
git clone https://github.com/attech-thanhnk/attech-client.git

# Kiểm tra
ls -la ~/projects/attech-server
ls -la ~/attech-client
```

---

## **BƯỚC 8: CẤU HÌNH .ENV.PRODUCTION**

⚡ **Đây là bước QUAN TRỌNG NHẤT - Chỉ cần config 1 file này!**

```bash
cd ~/projects/attech-server

# Copy file example
cp .env.production.example .env.production

# Edit file
nano .env.production
```

### **Nội dung cần chỉnh sửa:**

```bash
# ============================================
# DOMAIN CONFIGURATION (BẮT BUỘC)
# ============================================
FRONTEND_DOMAIN=attech.space              # ← Thay domain của bạn
FRONTEND_DOMAIN_WWW=www.attech.space      # ← Thay domain của bạn
API_DOMAIN=api.attech.space               # ← Thay domain của bạn

# ============================================
# SERVER PATHS (BẮT BUỘC - Chỉnh theo user của bạn)
# ============================================
FRONTEND_SOURCE_DIR=/home/thanhdev/attech-client              # ← Thay 'thanhdev'
UPLOADS_DIR=/home/thanhdev/projects/attech-server/uploads    # ← Thay 'thanhdev'

# ============================================
# SSL CERTIFICATE PATHS (GIỮ NGUYÊN)
# ============================================
SSL_CERT_BASE=/etc/letsencrypt/live
SSL_OPTIONS_FILE=/etc/letsencrypt/options-ssl-nginx.conf
SSL_DHPARAMS_FILE=/etc/letsencrypt/ssl-dhparams.pem

# ============================================
# DATABASE CONFIGURATION (BẮT BUỘC)
# ============================================
SA_PASSWORD=YourStrongPassword@123!       # ← Đổi password mạnh!
DB_NAME=AttechServerDb

# ============================================
# RESOURCE LIMITS (Tùy theo RAM VPS)
# ============================================
# Cấu hình cho VPS 4GB RAM (khuyến nghị):
BACKEND_MEM_LIMIT=1000
BACKEND_MEM_RESERVATION=600
FRONTEND_MEM_LIMIT=150
FRONTEND_MEM_RESERVATION=100
SQLSERVER_MEM_LIMIT=1200
SQLSERVER_MEM_RESERVATION=800
SQLSERVER_MEMORY_LIMIT_MB=1024

# Nếu VPS 8GB RAM, tăng lên:
# BACKEND_MEM_LIMIT=2000
# BACKEND_MEM_RESERVATION=1200
# SQLSERVER_MEM_LIMIT=2400
# SQLSERVER_MEMORY_LIMIT_MB=2048

# ============================================
# NGINX SETTINGS
# ============================================
CLIENT_MAX_BODY_SIZE=20M
ASPNETCORE_ENVIRONMENT=Production

# ============================================
# EMAIL CONFIGURATION
# ============================================
# OPTION 1: Để trống - Tự động tạo email từ domain
# Kết quả: admin@attech.space, support@attech.space, noreply@attech.space
ADMIN_EMAIL=
SUPPORT_EMAIL=
NOREPLY_EMAIL=

# OPTION 2: Dùng email riêng - Điền email cụ thể
# ADMIN_EMAIL=thanhnk@attech.com.vn
# SUPPORT_EMAIL=support@attech.com.vn
# NOREPLY_EMAIL=noreply@attech.com.vn

# SMTP Configuration (BẮT BUỘC để gửi email)
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=your-email@gmail.com        # ← Email Gmail của bạn
SMTP_PASSWORD=your-app-password           # ← App Password (không phải password Gmail)
SMTP_FROM_NAME=Công ty của bạn            # ← Tên hiển thị khi gửi email
```

**💡 Lưu ý về Gmail App Password:**
```
1. Vào: https://myaccount.google.com/apppasswords
2. Tạo App Password mới
3. Copy password và paste vào SMTP_PASSWORD
```

**Lưu file:** `Ctrl+O` → Enter → `Ctrl+X`

---

## **BƯỚC 9: CẤU HÌNH DNS**

Truy cập nhà cung cấp domain (Cloudflare, GoDaddy, NameCheap, etc) và tạo **3 DNS A Records**:

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | @ | YOUR_VPS_IP | Auto |
| A | www | YOUR_VPS_IP | Auto |
| A | api | YOUR_VPS_IP | Auto |

**Kiểm tra DNS đã trỏ đúng:**
```bash
ping attech.space
ping www.attech.space
ping api.attech.space
```

⏰ **Lưu ý:** DNS có thể mất 5-30 phút để propagate

---

## **BƯỚC 10: CÀI ĐẶT SSL CERTIFICATE (Let's Encrypt)**

```bash
# Cài Certbot
sudo apt install -y certbot

# Stop các container đang chạy (nếu có) để giải phóng port 80
cd ~/projects/attech-server
docker-compose -f docker-compose.fullstack.yml down || true

# Tạo SSL certificate cho TẤT CẢ 3 domains
sudo certbot certonly --standalone \
  -d attech.space \
  -d www.attech.space \
  -d api.attech.space \
  --email your-email@gmail.com \
  --agree-tos \
  --no-eff-email

# Kiểm tra certificates đã tạo thành công
ls -la /etc/letsencrypt/live/api.attech.space/
```

**Certbot sẽ hỏi:**
- Email: Nhập email của bạn
- Terms of Service: Gõ `Y`
- Share email with EFF: Gõ `N`

**Kết quả mong đợi:**
```
Successfully received certificate.
Certificate is saved at: /etc/letsencrypt/live/api.attech.space/fullchain.pem
Key is saved at:         /etc/letsencrypt/live/api.attech.space/privkey.pem
```

---

## **BƯỚC 11: DEPLOY PRODUCTION**

```bash
cd ~/projects/attech-server

# Chạy script deploy
sudo bash start-fullstack-production.sh
```

**Script sẽ tự động:**
1. ✅ Load configuration từ .env.production
2. ✅ Generate nginx config với domain của bạn
3. ✅ Generate docker-compose với environment variables
4. ✅ Stop các container cũ (nếu có)
5. ✅ Tạo thư mục uploads, logs
6. ✅ Kiểm tra SSL certificates
7. ✅ Build containers (Backend, Frontend)
8. ✅ Start tất cả services
9. ✅ Chờ services khởi động

**Output mong đợi:**
```
========================================
 ATTECH SERVER - PRODUCTION DEPLOYMENT
========================================

[1/9] Loading configuration... ✓
  → Frontend: attech.space
  → API: api.attech.space
[2/9] Generating nginx production config... ✓
[3/9] Generating docker-compose production config... ✓
[4/9] Stopping existing containers... ✓
[5/9] Creating necessary directories... ✓
[6/9] Checking SSL certificates... ✓
[7/9] Building containers... ✓
[8/9] Starting all services... ✓
[9/9] Waiting for services... ✓

========================================
 DEPLOYMENT COMPLETED!
========================================

  Frontend:  https://attech.space
  Backend:   https://api.attech.space

NAME                     STATUS
attechserver-db          Up (healthy)
attechserver-api         Up (healthy)
attechserver-frontend    Up (healthy)
attech-proxy             Up (healthy)
```

---

## **BƯỚC 12: KIỂM TRA HỆ THỐNG**

### **1. Kiểm tra containers:**
```bash
docker ps
```

**Kết quả mong đợi:** 4 containers đang chạy với status "Up (healthy)"

### **2. Kiểm tra logs:**
```bash
# Backend logs
docker logs attechserver-api --tail 50

# Frontend logs
docker logs attechserver-frontend --tail 50

# Proxy logs
docker logs attech-proxy --tail 50

# Database logs
docker logs attechserver-db --tail 50
```

### **3. Test API:**
```bash
# Health check
curl https://api.attech.space/health

# Test API endpoint
curl https://api.attech.space/api/news/client/find-all
```

**Kết quả mong đợi:** Status 200, trả về JSON data

### **4. Test Frontend:**
```bash
# Test homepage
curl https://attech.space

# Test với browser
# Mở: https://attech.space
```

### **5. Kiểm tra SSL:**
```bash
# Kiểm tra SSL certificate
curl -vI https://attech.space 2>&1 | grep "SSL certificate verify"
```

**Kết quả mong đợi:** "SSL certificate verify ok"

---

## **BƯỚC 13: RESTORE DATABASE (Nếu có backup)**

Nếu bạn có database backup từ local/production cũ:

```bash
# Copy file backup lên VPS (chạy từ máy local)
scp C:\path\to\backup.bak root@YOUR_VPS_IP:/home/thanhdev/

# Trên VPS, copy vào container
docker cp /home/thanhdev/backup.bak attechserver-db:/var/opt/mssql/backup.bak

# Restore database
docker exec -it attechserver-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_SA_PASSWORD' -C \
  -Q "RESTORE DATABASE AttechServerDb FROM DISK='/var/opt/mssql/backup.bak' WITH REPLACE"

# Restart backend để load lại data
docker restart attechserver-api
```

---

## **BƯỚC 14: COPY UPLOADS FOLDER (Nếu có)**

Nếu có folder uploads từ hệ thống cũ:

```bash
# Từ máy local, copy lên VPS
scp -r C:\path\to\Uploads root@YOUR_VPS_IP:/home/thanhdev/projects/attech-server/

# Hoặc dùng rsync (nhanh hơn)
rsync -avz C:/path/to/Uploads/ root@YOUR_VPS_IP:/home/thanhdev/projects/attech-server/uploads/

# Restart backend để nhận uploads mới
docker restart attechserver-api
```

---

## **BƯỚC 15: SETUP AUTO-RENEW SSL**

```bash
# Test auto-renewal
sudo certbot renew --dry-run

# Kiểm tra cronjob
systemctl status certbot.timer

# Xem lịch renew
sudo certbot certificates
```

Let's Encrypt SSL có hiệu lực 90 ngày, Certbot tự động renew khi còn 30 ngày.

---

## **BƯỚC 16: MONITORING & MAINTENANCE**

### **Xem logs real-time:**
```bash
# Backend logs
docker logs -f attechserver-api

# Tất cả logs
docker-compose -f docker-compose.fullstack.yml \
               -f docker-compose.fullstack.production.yml logs -f
```

### **Restart services:**
```bash
cd ~/projects/attech-server

# Restart toàn bộ
docker-compose -f docker-compose.fullstack.yml \
               -f docker-compose.fullstack.production.yml restart

# Restart từng service
docker restart attechserver-api
docker restart attechserver-frontend
docker restart attech-proxy
```

### **Update code mới:**
```bash
# Pull code backend
cd ~/projects/attech-server
git pull origin main

# Pull code frontend
cd ~/attech-client
git pull origin main

# Rebuild và deploy lại
cd ~/projects/attech-server
sudo bash start-fullstack-production.sh
```

### **Xem resource usage:**
```bash
# Xem CPU, RAM của containers
docker stats

# Xem disk usage
df -h
```

---

## 🔒 **BẢO MẬT BỔ SUNG (KHUYẾN NGHỊ)**

### **1. Đổi SSH Port:**
```bash
sudo nano /etc/ssh/sshd_config
```
Tìm và thay đổi:
```
Port 22        →  Port 2222
```
Restart SSH:
```bash
sudo systemctl restart ssh
sudo ufw allow 2222/tcp
sudo ufw delete allow OpenSSH
```

### **2. Disable Root Login:**
```bash
sudo nano /etc/ssh/sshd_config
```
Tìm và thay đổi:
```
PermitRootLogin yes  →  PermitRootLogin no
```
Restart:
```bash
sudo systemctl restart ssh
```

### **3. Setup Fail2ban (Chống brute force):**
```bash
sudo apt install -y fail2ban
sudo systemctl enable fail2ban
sudo systemctl start fail2ban

# Kiểm tra
sudo fail2ban-client status
```

### **4. Setup Automatic Security Updates:**
```bash
sudo apt install -y unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades
```

---

## 📝 **CHECKLIST SAU KHI DEPLOY**

- [ ] Frontend accessible: https://attech.space (200 OK)
- [ ] API accessible: https://api.attech.space/health (200 OK)
- [ ] SSL certificates valid (không có security warning)
- [ ] 4 containers đang chạy: db, api, frontend, proxy
- [ ] Tất cả containers status "healthy"
- [ ] Database có dữ liệu (hoặc đã restore)
- [ ] Uploads folder đã copy (nếu có)
- [ ] Logs không có error nghiêm trọng
- [ ] Email gửi được (test contact form)
- [ ] Admin panel login được
- [ ] DNS propagate đầy đủ (www, api)
- [ ] Auto-renew SSL đã setup

---

## 🆘 **TROUBLESHOOTING**

### **Lỗi 1: Port 80/443 đã được sử dụng**
```bash
# Tìm process đang dùng port
sudo lsof -i :80
sudo lsof -i :443

# Nếu là nginx/apache cũ, stop nó
sudo systemctl stop nginx
sudo systemctl stop apache2

# Disable khởi động cùng hệ thống
sudo systemctl disable nginx
sudo systemctl disable apache2
```

### **Lỗi 2: SSL certificate not found**
```bash
# Xem logs certbot
sudo journalctl -u certbot -n 50

# Xem certificates hiện có
sudo certbot certificates

# Thử tạo lại
sudo certbot certonly --standalone \
  -d attech.space \
  -d www.attech.space \
  -d api.attech.space \
  --force-renewal
```

### **Lỗi 3: Container không start được**
```bash
# Xem logs chi tiết
docker logs attechserver-api

# Xem events
docker events --since 5m

# Kiểm tra cấu hình
docker inspect attechserver-api

# Xem resource usage
docker stats
```

### **Lỗi 4: Database connection failed**
```bash
# Kiểm tra database container
docker logs attechserver-db

# Kiểm tra connection string
docker exec attechserver-api env | grep ConnectionStrings

# Test kết nối database
docker exec -it attechserver-db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YOUR_PASSWORD' -C -Q "SELECT @@VERSION"
```

### **Lỗi 5: Frontend không load được**
```bash
# Kiểm tra frontend logs
docker logs attechserver-frontend

# Kiểm tra nginx proxy
docker logs attech-proxy

# Test từng service
curl http://localhost:80  # Test trực tiếp frontend container
```

### **Lỗi 6: CORS Error**
```bash
# Kiểm tra CORS config
docker exec attechserver-api env | grep CorsOrigins

# Restart backend
docker restart attechserver-api
```

### **Lỗi 7: Email không gửi được**
```bash
# Kiểm tra SMTP config
docker exec attechserver-api env | grep -i smtp

# Test SMTP connection
docker exec attechserver-api bash -c "curl -v telnet://smtp.gmail.com:587"
```

### **Lỗi 8: Out of memory**
```bash
# Xem memory usage
free -h
docker stats

# Giảm memory limits trong .env.production
# Restart lại
```

---

## 📞 **HỖ TRỢ**

Nếu gặp vấn đề không giải quyết được:

1. Xem logs chi tiết: `docker logs <container-name>`
2. Check GitHub Issues: https://github.com/attech-thanhnk/attech-server/issues
3. Liên hệ: tonynguyendev1@gmail.com

---

## 🎉 **HOÀN THÀNH!**

Chúc mừng! Hệ thống của bạn đã chạy production trên VPS.

**Các bước tiếp theo:**
- Setup backup tự động cho database
- Setup monitoring (Grafana, Prometheus)
- Configure CDN (Cloudflare)
- Setup logging tập trung
- Performance tuning

---

**Cập nhật cuối:** 2025-01-13
**Version:** 1.0
