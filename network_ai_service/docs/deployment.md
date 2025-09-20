# Network AI Service - 배포 가이드

## 🚀 배포 환경 준비

### 시스템 요구사항

#### 최소 사양

- **CPU**: 2 코어 이상
- **메모리**: 4GB RAM 이상
- **저장공간**: 10GB 이상 여유 공간
- **네트워크**: 인터넷 연결 (패키지 다운로드용)

#### 권장 사양

- **CPU**: 4 코어 이상 (Intel i5/AMD Ryzen 5 급 이상)
- **메모리**: 8GB RAM 이상
- **저장공간**: SSD 20GB 이상
- **네트워크**: 기가비트 이더넷

#### 운영체제

- **Windows**: Windows 10/11 (64비트)
- **Linux**: Ubuntu 20.04 LTS 이상, CentOS 8 이상
- **Python**: 3.9 이상

## 🐳 Docker를 이용한 배포

### 1. Docker 설치

```bash
# Windows (PowerShell 관리자 권한)
winget install Docker.DockerDesktop

# Linux (Ubuntu)
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
```

### 2. 프로젝트 클론 및 빌드

```bash
cd c:\My_Project\WS\network_ai_service

# Docker 이미지 빌드
docker build -t network-ai-service:latest .

# 빌드 확인
docker images | grep network-ai-service
```

### 3. Docker Compose로 서비스 실행

```bash
# 서비스 시작
docker-compose up -d

# 서비스 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f network-ai-service
```

### 4. 서비스 접속 확인

```bash
# API 헬스 체크
curl http://localhost:8000/api/v1/health

# Swagger UI 접속
# http://localhost:8000/docs
```

## 🔧 수동 배포 (Python 가상환경)

### 1. 프로젝트 준비

```bash
# 프로젝트 디렉토리로 이동
cd c:\My_Project\WS\network_ai_service

# 가상환경 생성
python -m venv venv

# 가상환경 활성화 (Windows)
venv\Scripts\activate

# 가상환경 활성화 (Linux)
source venv/bin/activate

# 의존성 설치
pip install -r requirements.txt
```

### 2. 환경 설정

```bash
# 환경 변수 파일 생성
copy config\production.yaml.example config\production.yaml

# 데이터베이스 초기화
python scripts\init_database.py --env production

# 모델 디렉토리 생성
mkdir models data logs
```

### 3. 서비스 시작

```bash
# 개발 서버 (테스트용)
python -m uvicorn src.api.main:app --host 0.0.0.0 --port 8000

# 프로덕션 서버 (Gunicorn 사용)
gunicorn src.api.main:app -w 4 -k uvicorn.workers.UvicornWorker --bind 0.0.0.0:8000
```

## ⚙️ 환경별 설정

### 개발 환경 (config/development.yaml)

```yaml
environment: development
debug: true
log_level: DEBUG

api:
  host: "0.0.0.0"
  port: 8000
  reload: true
  workers: 1

database:
  url: "sqlite:///./data/network_ai_dev.db"
  echo: true

ml_models:
  auto_retrain: false
  retrain_interval_hours: 24
  model_store_path: "./models"

data_collection:
  interval_seconds: 5.0
  buffer_size: 100
  enable_packet_capture: false

monitoring:
  enable_prometheus: true
  prometheus_port: 9090
  log_file: "./logs/network_ai_dev.log"
```

### 프로덕션 환경 (config/production.yaml)

```yaml
environment: production
debug: false
log_level: INFO

api:
  host: "0.0.0.0"
  port: 8000
  reload: false
  workers: 4

database:
  url: "sqlite:///./data/network_ai.db"
  echo: false
  pool_size: 10
  max_overflow: 20

ml_models:
  auto_retrain: true
  retrain_interval_hours: 168 # 7 days
  model_store_path: "./models"
  backup_old_models: true

data_collection:
  interval_seconds: 1.0
  buffer_size: 1000
  enable_packet_capture: true
  max_connections_per_scan: 10000

security:
  api_key_required: true
  jwt_secret_key: "your-secret-key-here"
  jwt_expire_hours: 24
  cors_origins: ["http://localhost:3000"]

monitoring:
  enable_prometheus: true
  prometheus_port: 9090
  log_file: "./logs/network_ai.log"
  log_rotation: true
  max_log_size_mb: 100
  backup_count: 5

performance:
  max_concurrent_requests: 100
  request_timeout_seconds: 30
  cache_ttl_seconds: 300
```

## 🔐 보안 설정

### 1. API 키 생성

```bash
# API 키 생성 스크립트 실행
python scripts/generate_api_key.py

# 출력 예시
# API Key: ai_service_key_abc123def456
# JWT Secret: jwt_secret_xyz789uvw
```

### 2. SSL/TLS 설정 (프로덕션)

```bash
# Let's Encrypt 인증서 생성 (Linux)
sudo apt install certbot
sudo certbot certonly --standalone -d your-domain.com

# Windows에서는 IIS 또는 nginx 프록시 사용 권장
```

### 3. 방화벽 설정

```bash
# Windows 방화벽 (PowerShell 관리자 권한)
New-NetFirewallRule -DisplayName "Network AI Service" -Direction Inbound -Port 8000 -Protocol TCP -Action Allow

# Linux iptables
sudo iptables -A INPUT -p tcp --dport 8000 -j ACCEPT
sudo iptables-save
```

## 📊 모니터링 설정

### 1. Prometheus 설정

```yaml
# config/prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: "network-ai-service"
    static_configs:
      - targets: ["localhost:8000"]
    metrics_path: "/metrics"
    scrape_interval: 5s
```

### 2. Grafana 대시보드

```bash
# Grafana 컨테이너 시작
docker run -d -p 3000:3000 --name grafana grafana/grafana

# 기본 로그인: admin/admin
# URL: http://localhost:3000

# 데이터 소스 추가: Prometheus (http://localhost:9090)
```

### 3. 알림 설정

```python
# scripts/setup_alerts.py
import requests

SLACK_WEBHOOK_URL = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"

def send_alert(message: str, severity: str = "warning"):
    payload = {
        "text": f"🚨 Network AI Service Alert [{severity.upper()}]",
        "attachments": [
            {
                "color": "danger" if severity == "critical" else "warning",
                "text": message,
                "ts": int(time.time())
            }
        ]
    }
    requests.post(SLACK_WEBHOOK_URL, json=payload)
```

## 🔄 자동 배포 설정

### 1. GitHub Actions 워크플로우

```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [main]
    tags: ["v*"]

jobs:
  deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3

      - name: Set up Python
        uses: actions/setup-python@v4
        with:
          python-version: 3.9

      - name: Run tests
        run: |
          pip install -r requirements.txt
          pip install -r requirements-dev.txt
          pytest

      - name: Build Docker image
        run: |
          docker build -t network-ai-service:${{ github.sha }} .
          docker tag network-ai-service:${{ github.sha }} network-ai-service:latest

      - name: Deploy to server
        run: |
          # SSH를 통한 원격 배포
          echo "${{ secrets.DEPLOY_KEY }}" | tr -d '\r' > deploy_key
          chmod 600 deploy_key
          ssh -i deploy_key -o StrictHostKeyChecking=no ${{ secrets.DEPLOY_USER }}@${{ secrets.DEPLOY_HOST }} << 'EOF'
            cd /opt/network-ai-service
            docker-compose pull
            docker-compose up -d
            docker system prune -f
          EOF
```

### 2. 무중단 배포 스크립트

```bash
#!/bin/bash
# scripts/deploy.sh

set -e

SERVICE_NAME="network-ai-service"
BACKUP_NAME="${SERVICE_NAME}-backup"

echo "🚀 Starting deployment..."

# 현재 서비스 백업
echo "📦 Creating backup..."
docker tag $SERVICE_NAME:latest $BACKUP_NAME:latest || echo "No existing image to backup"

# 새 이미지 빌드
echo "🔨 Building new image..."
docker build -t $SERVICE_NAME:latest .

# 헬스체크 함수
health_check() {
    local max_attempts=30
    local attempt=0

    while [ $attempt -lt $max_attempts ]; do
        if curl -s http://localhost:8000/api/v1/health | grep -q "healthy"; then
            echo "✅ Health check passed"
            return 0
        fi

        echo "⏳ Waiting for service to be ready... ($((attempt + 1))/$max_attempts)"
        sleep 2
        ((attempt++))
    done

    echo "❌ Health check failed"
    return 1
}

# 서비스 재시작
echo "🔄 Restarting service..."
docker-compose up -d $SERVICE_NAME

# 헬스체크
if health_check; then
    echo "✅ Deployment successful!"
    # 백업 이미지 정리
    docker rmi $BACKUP_NAME:latest 2>/dev/null || echo "No backup to clean"
else
    echo "❌ Deployment failed, rolling back..."
    # 롤백
    docker tag $BACKUP_NAME:latest $SERVICE_NAME:latest
    docker-compose up -d $SERVICE_NAME

    if health_check; then
        echo "✅ Rollback successful"
    else
        echo "💥 Rollback failed - manual intervention required!"
        exit 1
    fi
fi

echo "📊 Current service status:"
docker-compose ps $SERVICE_NAME
```

## 🔧 운영 관리

### 1. 로그 관리

```bash
# 로그 확인
docker-compose logs -f network-ai-service

# 로그 로테이션 설정 (Linux)
sudo tee /etc/logrotate.d/network-ai-service << EOF
/opt/network-ai-service/logs/*.log {
    daily
    missingok
    rotate 7
    compress
    delaycompress
    notifempty
    copytruncate
}
EOF
```

### 2. 백업 설정

```bash
#!/bin/bash
# scripts/backup.sh

BACKUP_DIR="/opt/backups/network-ai-service"
DATE=$(date +%Y%m%d_%H%M%S)

# 데이터베이스 백업
echo "🗄️ Backing up database..."
mkdir -p $BACKUP_DIR
cp data/network_ai.db $BACKUP_DIR/network_ai_$DATE.db

# 모델 백업
echo "🧠 Backing up models..."
tar -czf $BACKUP_DIR/models_$DATE.tar.gz models/

# 설정 백업
echo "⚙️ Backing up configurations..."
tar -czf $BACKUP_DIR/config_$DATE.tar.gz config/

# 오래된 백업 정리 (30일 이상)
echo "🧹 Cleaning old backups..."
find $BACKUP_DIR -type f -mtime +30 -delete

echo "✅ Backup completed: $DATE"
```

### 3. 성능 모니터링

```bash
# 시스템 리소스 모니터링
docker stats network-ai-service

# API 성능 테스트
ab -n 1000 -c 10 http://localhost:8000/api/v1/health

# 데이터베이스 크기 확인
du -sh data/network_ai.db
```

## 🚨 장애 대응

### 일반적인 문제 해결

#### 1. 서비스가 시작되지 않는 경우

```bash
# 로그 확인
docker-compose logs network-ai-service

# 포트 충돌 확인
netstat -ano | findstr :8000

# 권한 문제 확인 (Linux)
ls -la data/ models/ logs/
```

#### 2. 메모리 부족

```bash
# 메모리 사용량 확인
docker stats

# 가비지 컬렉션 강제 실행
curl -X POST http://localhost:8000/api/v1/admin/gc

# 컨테이너 재시작
docker-compose restart network-ai-service
```

#### 3. 데이터베이스 문제

```bash
# 데이터베이스 무결성 검사
python scripts/check_database.py

# 데이터베이스 복구
python scripts/repair_database.py

# 백업에서 복원
cp /opt/backups/network-ai-service/network_ai_20250918_140000.db data/network_ai.db
```

### 비상 연락처 및 절차

1. **시스템 관리자**: admin@yourcompany.com
2. **개발팀 리더**: dev-lead@yourcompany.com
3. **24시간 모니터링**: monitoring@yourcompany.com

### 복구 절차

1. 즉시 백업에서 복구
2. 문제 원인 분석
3. 임시 해결책 적용
4. 근본 원인 해결
5. 재발 방지 조치

이 배포 가이드를 통해 안정적이고 확장 가능한 Network AI Service를 운영할 수 있습니다.
