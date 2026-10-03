# Triển khai WebShop lên VPS

Tài liệu cho VPS nhỏ (vd. Cloud Server #0: 1–2GB RAM), **không dùng Docker/n8n** để giữ máy nhẹ.

Stack: **ASP.NET Core 8** + **SQLite** + **Nginx** (HTTPS) + **systemd** + (tuỳ chọn) **GitHub Actions**.

---

## 1. Chuẩn bị

| Mục | Ghi chú |
|-----|---------|
| VPS | Ubuntu 22.04/24.04 LTS khuyến nghị |
| Domain | A record trỏ về IP VPS (vd. `gomsuluxury.vn`) |
| Port | Mở **22** (SSH), **80**, **443** |
| Local | Đã tắt sync nặng trong `appsettings.Local.json` (`Seed.SourceSiteSync: false`) |

**Không cài** ứng dụng mẫu n8n/Docker trên panel nếu chỉ chạy WebShop.

---

## 2. Cài đặt lần đầu trên VPS

SSH vào máy (thay user/host):

```bash
ssh deploy@YOUR_VPS_IP
```

### 2.1. Gói hệ thống

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y nginx certbot python3-certbot-nginx ufw
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx Full'
sudo ufw enable
```

### 2.2. .NET 8 ASP.NET Core Runtime

Theo [Microsoft](https://learn.microsoft.com/dotnet/core/install/linux-ubuntu):

```bash
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
dotnet --list-runtimes
```

### 2.3. User và thư mục

```bash
sudo adduser --system --group --home /var/www/webshop webshop
sudo mkdir -p /var/www/webshop/{app,data,backups,releases}
sudo chown -R webshop:webshop /var/www/webshop
```

- `app` — bản đang chạy (symlink hoặc nội dung publish)
- `data` — `webshop.db` + có thể gắn `uploads`
- `releases` — các bản publish theo thời gian (rollback)
- `backups` — sao lưu DB/uploads

---

## 3. Publish từ máy dev (lần đầu / thủ công)

Trên máy Windows (thư mục repo):

```powershell
cd D:\C#\webshop\WebShop
dotnet publish -c Release -o .\artifacts\publish
```

Copy lên VPS (ví dụ `scp`/WinSCP):

```text
artifacts/publish/*  →  /var/www/webshop/releases/YYYYMMDD-HHMMSS/
```

Rồi trên VPS:

```bash
REL=/var/www/webshop/releases/YYYYMMDD-HHMMSS
sudo ln -sfn "$REL" /var/www/webshop/app
# Đảm bảo file tĩnh ACE (nếu deploy kèm) hoặc copy thư mục ace cạnh app theo cách bạn chọn
sudo chown -R webshop:webshop /var/www/webshop
```

### SQLite & uploads

Khuyến nghị Production:

- Connection string: `Data Source=/var/www/webshop/data/webshop.db`
- Uploads: giữ dưới `app/wwwroot/uploads` **hoặc** symlink/mount tới `/var/www/webshop/data/uploads` để không mất khi đổi release

Lần đầu: copy DB local đã backup (nếu có) vào `data/`, hoặc để app tự tạo DB trống + `AdminSeed` (`admin` / chỉ lần đầu — **đổi mật khẩu ngay**).

File cấu hình Production (không commit secret): xem `docs/deploy/appsettings.Production.json.example`  
Đặt trên server: `/var/www/webshop/app/appsettings.Production.json`  
(hoặc `/var/www/webshop/data/appsettings.Production.json` rồi copy/symlink vào thư mục app mỗi lần deploy).

Biến môi trường:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
```

App **chỉ listen localhost**; Nginx proxy ra ngoài.

---

## 4. systemd

File mẫu: `docs/deploy/webshop.service`

```bash
sudo cp /path/to/repo/docs/deploy/webshop.service /etc/systemd/system/webshop.service
sudo systemctl daemon-reload
sudo systemctl enable --now webshop
sudo systemctl status webshop
```

Lệnh thường dùng:

```bash
sudo systemctl restart webshop
sudo journalctl -u webshop -f
```

---

## 5. Nginx + SSL

1. Copy `docs/deploy/nginx-webshop.conf`, sửa `server_name` thành domain thật.
2. Cài site:

```bash
sudo cp nginx-webshop.conf /etc/nginx/sites-available/webshop
sudo ln -s /etc/nginx/sites-available/webshop /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

3. Cấp chứng chỉ Let’s Encrypt:

```bash
sudo certbot --nginx -d gomsuluxury.vn -d www.gomsuluxury.vn
```

Certbot tự sửa block SSL và gia hạn (timer). Kiểm tra: `sudo certbot renew --dry-run`.

**DNS:** A/AAAA phải trỏ đúng IP VPS trước khi chạy certbot.

---

## 6. Thư mục `ace` (theme admin)

App mount static `/ace` từ `../ace` so với ContentRoot. Khi publish:

- Hoặc copy thư mục `ace` lên `/var/www/webshop/ace` và chỉnh code/path nếu cần,  
- Hoặc đặt `ace` cạnh release: `/var/www/webshop/releases/.../../ace` theo cấu trúc repo (`WebShop` + `ace` ngang hàng).

Cấu trúc gợi ý trên VPS:

```text
/var/www/webshop/
  ace/                 # copy từ repo /ace
  app -> releases/...  # ContentRoot = app; Program tìm ../ace
  releases/
    20261003-120000/   # publish output (WebShop)
  data/
    webshop.db
    uploads/           # optional symlink vào app/wwwroot/uploads
```

Với symlink `app` → `releases/xxx`, thì `../ace` từ ContentRoot (`.../releases/xxx`) **không** ra `/var/www/webshop/ace`.  

**Cách đơn giản:** mỗi release đặt thêm symlink:

```bash
ln -sfn /var/www/webshop/ace "$REL/../ace"
# Không ổn với path. Tốt hơn: symlink trong parent cố định:
# ContentRoot = /var/www/webshop/app  (thư mục thật, không phải deep release)
```

**Khuyến nghị thực tế:** mỗi lần deploy **rsync publish vào** `/var/www/webshop/app` (thư mục cố định), giữ `ace` tại `/var/www/webshop/ace`, và sửa cấu hình nếu path `../ace` không khớp — hoặc copy `ace` vào `/var/www/webshop/` đúng quan hệ `app` + `ace` như repo:

```text
/var/www/webshop/
  WebShop/     # = publish (ContentRoot)
  ace/
```

Đặt systemd `WorkingDirectory=/var/www/webshop/WebShop` thì `../ace` khớp repo.

---

## 7. Seed / Production config

Trong `appsettings.Production.json` (server):

```json
"Seed": {
  "DemoCatalog": false,
  "LuxuryCatalog": false,
  "SourceSiteSync": false,
  "RemoteImageImport": false
},
"AllowedHosts": "gomsuluxury.vn;www.gomsuluxury.vn"
```

`AdminSeed` vẫn tạo `admin` **chỉ khi chưa có user** — không ghi đè mật khẩu đã đổi.

---

## 8. CI/CD (GitHub Actions) — tuỳ chọn

Mẫu workflow: `docs/deploy/github-actions-deploy.yml`

Secrets trên GitHub repo:

| Secret | Ý nghĩa |
|--------|---------|
| `VPS_HOST` | IP hoặc hostname |
| `VPS_USER` | user SSH (vd. `deploy`) |
| `VPS_SSH_KEY` | private key |
| `VPS_PORT` | tuỳ chọn, mặc định 22 |

Luồng: push `main` → `dotnet publish` → `rsync`/`scp` lên VPS → `systemctl restart webshop`.

User SSH cần quyền `sudo systemctl restart webshop` không mật khẩu (sudoers) hoặc dùng script deploy thuộc user `webshop`.

---

## 9. Backup & rollback

### Backup (cron mỗi đêm)

```bash
#!/bin/bash
set -euo pipefail
STAMP=$(date +%Y%m%d)
DEST=/var/www/webshop/backups/$STAMP
mkdir -p "$DEST"
systemctl stop webshop
cp -a /var/www/webshop/data/webshop.db* "$DEST/" 2>/dev/null || true
# nếu uploads trong data:
cp -a /var/www/webshop/data/uploads "$DEST/" 2>/dev/null || true
systemctl start webshop
find /var/www/webshop/backups -mindepth 1 -maxdepth 1 -mtime +14 -exec rm -rf {} \;
```

Gợi ý: copy thêm backup ra máy khác / Object Storage.

### Rollback

Giữ thư mục release cũ hoặc bản backup `app`; trỏ lại / rsync bản trước → `systemctl restart webshop`.

---

### Uploads / media

- Thư mục `wwwroot/uploads` **không** nằm trong git và **không** deploy qua CI.
- Đồng bộ lần đầu / khi thiếu ảnh: copy từ máy local lên `/var/www/webshop/WebShop/wwwroot/uploads`.
- CI dùng `rsync --delete` nhưng **protect/exclude** `wwwroot/uploads` — vẫn kiểm tra sau mỗi deploy nếu nghi ngờ mất file.

---

## 10. Checklist sau deploy

- [ ] `https://domain` mở được, chứng chỉ xanh  
- [ ] `/san-pham`, giỏ hàng, trang tĩnh `/{slug}`  
- [ ] `/Login` → Admin; đã đổi mật khẩu `admin`  
- [ ] Upload ảnh trong Media  
- [ ] `Seed.SourceSiteSync` = false trên Production  
- [ ] Backup DB chạy thử một lần  
- [ ] `sudo systemctl status webshop` active  

---

## 11. Khi nhờ Agent triển khai hộ

Chuẩn bị gửi (riêng tư, không commit vào git):

1. IP VPS, user SSH, cách đăng nhập (key)  
2. Domain đã trỏ DNS  
3. Repo remote (nếu dùng CI)  
4. Có mang DB/uploads từ local lên không  

Agent có thể SSH cài runtime, copy publish, Nginx, certbot, systemd theo tài liệu này.

---

## File kèm trong `docs/deploy/`

| File | Mục đích |
|------|----------|
| `webshop.service` | systemd unit |
| `nginx-webshop.conf` | Nginx reverse proxy (HTTP; certbot thêm SSL) |
| `appsettings.Production.json.example` | Mẫu cấu hình Production |
| `github-actions-deploy.yml` | Mẫu CI/CD |

Cập nhật tài liệu khi đổi path, domain, hoặc cách publish.
