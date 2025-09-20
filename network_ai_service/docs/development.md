# Network AI Service - 개발 가이드

## 🚀 개발 환경 설정

### 시스템 요구사항

- **Python**: 3.9 이상
- **운영체제**: Windows 10/11 (리눅스 지원 예정)
- **메모리**: 최소 4GB, 권장 8GB
- **디스크**: 최소 2GB 여유 공간

### 개발 환경 구축

#### 1. 프로젝트 클론 및 가상환경 설정

```bash
cd c:\My_Project\WS\network_ai_service

# 가상환경 생성
python -m venv venv

# 가상환경 활성화 (Windows)
venv\Scripts\activate

# 가상환경 활성화 (Linux/Mac)
source venv/bin/activate

# 의존성 설치
pip install -r requirements.txt
```

#### 2. 개발용 패키지 설치

```bash
pip install -r requirements-dev.txt
```

### 프로젝트 구조

```
network_ai_service/
├── src/                          # 소스 코드
│   ├── __init__.py
│   ├── main.py                   # 애플리케이션 진입점
│   │
│   ├── api/                      # FastAPI 관련
│   │   ├── __init__.py
│   │   ├── main.py              # FastAPI 앱 생성
│   │   ├── endpoints/           # API 엔드포인트
│   │   │   ├── __init__.py
│   │   │   ├── analysis.py      # 분석 관련 API
│   │   │   ├── models.py        # 모델 관련 API
│   │   │   └── system.py        # 시스템 관련 API
│   │   └── middleware/          # 미들웨어
│   │       ├── __init__.py
│   │       ├── auth.py          # 인증 미들웨어
│   │       └── logging.py       # 로깅 미들웨어
│   │
│   ├── collectors/              # 데이터 수집
│   │   ├── __init__.py
│   │   ├── base.py             # 기본 수집기 클래스
│   │   ├── network_collector.py # 네트워크 데이터 수집
│   │   ├── process_collector.py # 프로세스 데이터 수집
│   │   └── traffic_analyzer.py  # 트래픽 분석
│   │
│   ├── models/                  # ML 모델
│   │   ├── __init__.py
│   │   ├── base_model.py       # 기본 모델 클래스
│   │   ├── baseline_learner.py # 베이스라인 학습
│   │   ├── anomaly_detector.py # 이상 탐지 모델
│   │   └── ensemble.py         # 앙상블 모델
│   │
│   ├── pipeline/               # 데이터 파이프라인
│   │   ├── __init__.py
│   │   ├── data_processor.py   # 데이터 전처리
│   │   ├── feature_engineer.py # 특징 엔지니어링
│   │   └── model_trainer.py    # 모델 학습
│   │
│   ├── storage/                # 데이터 저장
│   │   ├── __init__.py
│   │   ├── database.py         # 데이터베이스 관리
│   │   ├── model_store.py      # 모델 저장소
│   │   └── cache.py            # 캐시 관리
│   │
│   └── utils/                  # 유틸리티
│       ├── __init__.py
│       ├── config.py           # 설정 관리
│       ├── logger.py           # 로깅 설정
│       ├── metrics.py          # 메트릭 수집
│       └── exceptions.py       # 사용자 정의 예외
│
├── tests/                      # 테스트 코드
│   ├── __init__.py
│   ├── conftest.py            # pytest 설정
│   ├── unit/                  # 단위 테스트
│   ├── integration/           # 통합 테스트
│   └── fixtures/              # 테스트 데이터
│
├── scripts/                    # 스크립트
│   ├── init_database.py       # DB 초기화
│   ├── train_models.py        # 모델 학습 스크립트
│   └── generate_test_data.py  # 테스트 데이터 생성
│
├── config/                     # 설정 파일
│   ├── development.yaml
│   ├── production.yaml
│   └── test.yaml
│
├── models/                     # 저장된 모델 파일
├── data/                       # 데이터 파일
├── logs/                       # 로그 파일
├── docs/                       # 문서
├── requirements.txt            # 운영 의존성
├── requirements-dev.txt        # 개발 의존성
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## 🛠️ 개발 도구 및 컨벤션

### 코드 품질 도구

#### 1. Code Formatting

```bash
# Black: 코드 포매터
black src/ tests/

# isort: import 정렬
isort src/ tests/
```

#### 2. Code Linting

```bash
# flake8: 린팅
flake8 src/ tests/

# pylint: 상세 린팅
pylint src/
```

#### 3. Type Checking

```bash
# mypy: 타입 체크
mypy src/
```

### Pre-commit Hooks

`.pre-commit-config.yaml`:

```yaml
repos:
  - repo: https://github.com/psf/black
    rev: 22.3.0
    hooks:
      - id: black
        language_version: python3.9

  - repo: https://github.com/pycqa/isort
    rev: 5.10.1
    hooks:
      - id: isort

  - repo: https://github.com/pycqa/flake8
    rev: 4.0.1
    hooks:
      - id: flake8

  - repo: https://github.com/pre-commit/mirrors-mypy
    rev: v0.942
    hooks:
      - id: mypy
```

## 📋 개발 가이드라인

### 코딩 스타일

#### 1. 함수 및 클래스 명명

```python
# 좋은 예
class NetworkDataCollector:
    async def collect_active_connections(self) -> List[ConnectionData]:
        pass

# 나쁜 예
class NDC:
    def get_data(self):
        pass
```

#### 2. 타입 힌트 사용

```python
from typing import List, Dict, Optional, Union
from datetime import datetime

class ConnectionAnalyzer:
    def analyze_connection(
        self,
        connection: ConnectionData,
        historical_data: Optional[List[ConnectionData]] = None
    ) -> AnalysisResult:
        """네트워크 연결 분석"""
        pass
```

#### 3. 에러 처리

```python
from src.utils.exceptions import CollectionError, ModelError

class NetworkCollector:
    async def collect_data(self) -> List[ConnectionData]:
        try:
            # 데이터 수집 로직
            return data
        except psutil.Error as e:
            raise CollectionError(f"Failed to collect network data: {e}") from e
        except Exception as e:
            self.logger.error(f"Unexpected error: {e}")
            raise
```

### 비동기 프로그래밍 패턴

#### 1. AsyncIO 사용

```python
import asyncio
from typing import AsyncGenerator

class RealTimeCollector:
    async def collect_stream(self) -> AsyncGenerator[ConnectionData, None]:
        """실시간 데이터 스트림"""
        while True:
            try:
                data = await self._collect_current_data()
                yield data
                await asyncio.sleep(self.collection_interval)
            except Exception as e:
                self.logger.error(f"Collection error: {e}")
                await asyncio.sleep(1)  # 재시도 전 대기
```

#### 2. 리소스 관리

```python
import aiofiles
from contextlib import asynccontextmanager

@asynccontextmanager
async def database_transaction():
    """데이터베이스 트랜잭션 컨텍스트 매니저"""
    conn = await get_database_connection()
    try:
        async with conn.begin():
            yield conn
    except Exception:
        await conn.rollback()
        raise
    finally:
        await conn.close()
```

## 🧪 테스트 작성 가이드

### 테스트 구조

#### 1. 단위 테스트

```python
import pytest
from unittest.mock import AsyncMock, patch
from src.collectors.network_collector import NetworkCollector

class TestNetworkCollector:
    @pytest.fixture
    def collector(self):
        return NetworkCollector(collection_interval=1.0)

    @pytest.mark.asyncio
    async def test_collect_connections_success(self, collector):
        """정상적인 연결 수집 테스트"""
        with patch('psutil.net_connections') as mock_connections:
            mock_connections.return_value = [
                # Mock connection data
            ]

            result = await collector.collect_connections()

            assert len(result) > 0
            assert all(isinstance(conn, ConnectionData) for conn in result)

    @pytest.mark.asyncio
    async def test_collect_connections_error_handling(self, collector):
        """에러 처리 테스트"""
        with patch('psutil.net_connections', side_effect=psutil.Error("Mock error")):
            with pytest.raises(CollectionError):
                await collector.collect_connections()
```

#### 2. 통합 테스트

```python
import pytest
from httpx import AsyncClient
from src.api.main import app

class TestAnalysisAPI:
    @pytest.mark.asyncio
    async def test_analyze_endpoint(self):
        """분석 API 통합 테스트"""
        async with AsyncClient(app=app, base_url="http://test") as client:
            test_data = {
                "process_id": 1234,
                "process_name": "test.exe",
                # ... 기타 필드
            }

            response = await client.post("/api/v1/analyze", json=test_data)

            assert response.status_code == 200
            result = response.json()
            assert "is_anomaly" in result["result"]
            assert "anomaly_score" in result["result"]
```

### 테스트 실행

```bash
# 모든 테스트 실행
pytest

# 특정 테스트 실행
pytest tests/unit/test_collectors.py::TestNetworkCollector::test_collect_connections

# 커버리지 포함 실행
pytest --cov=src --cov-report=html

# 병렬 실행
pytest -n 4
```

## 🔧 개발 도구

### IDE 설정 (VS Code)

#### settings.json

```json
{
  "python.defaultInterpreterPath": "./venv/Scripts/python.exe",
  "python.formatting.provider": "black",
  "python.linting.enabled": true,
  "python.linting.flake8Enabled": true,
  "python.linting.mypyEnabled": true,
  "editor.formatOnSave": true
}
```

#### 확장 프로그램

- Python
- Pylance
- Python Docstring Generator
- GitLens
- Thunder Client (API 테스트)

### 디버깅 설정

#### launch.json

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Python: FastAPI",
      "type": "python",
      "request": "launch",
      "module": "uvicorn",
      "args": [
        "src.api.main:app",
        "--host",
        "0.0.0.0",
        "--port",
        "8000",
        "--reload"
      ],
      "console": "integratedTerminal",
      "cwd": "${workspaceFolder}"
    },
    {
      "name": "Python: Current File",
      "type": "python",
      "request": "launch",
      "program": "${file}",
      "console": "integratedTerminal",
      "cwd": "${workspaceFolder}"
    }
  ]
}
```

## 📊 성능 및 모니터링

### 프로파일링

```python
import cProfile
import pstats
from functools import wraps

def profile_function(func):
    """함수 프로파일링 데코레이터"""
    @wraps(func)
    async def wrapper(*args, **kwargs):
        pr = cProfile.Profile()
        pr.enable()
        try:
            result = await func(*args, **kwargs)
            return result
        finally:
            pr.disable()
            stats = pstats.Stats(pr)
            stats.sort_stats('cumulative')
            stats.print_stats(10)  # 상위 10개 출력
    return wrapper
```

### 메트릭 수집

```python
from prometheus_client import Counter, Histogram, start_http_server
import time

# 메트릭 정의
request_count = Counter('requests_total', 'Total requests', ['method', 'endpoint'])
request_duration = Histogram('request_duration_seconds', 'Request duration')

def track_performance(func):
    """성능 추적 데코레이터"""
    @wraps(func)
    async def wrapper(*args, **kwargs):
        start_time = time.time()
        try:
            result = await func(*args, **kwargs)
            return result
        finally:
            duration = time.time() - start_time
            request_duration.observe(duration)
    return wrapper
```

## 🚀 배포 준비

### Docker 설정

#### Dockerfile

```dockerfile
FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY src/ ./src/
COPY config/ ./config/

EXPOSE 8000

CMD ["uvicorn", "src.api.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

#### docker-compose.yml

```yaml
version: "3.8"

services:
  network-ai-service:
    build: .
    ports:
      - "8000:8000"
    environment:
      - ENVIRONMENT=production
      - DATABASE_URL=sqlite:///./data/network_ai.db
    volumes:
      - ./data:/app/data
      - ./models:/app/models
      - ./logs:/app/logs
    restart: unless-stopped

  prometheus:
    image: prom/prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./config/prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
```

### CI/CD 파이프라인

#### GitHub Actions (.github/workflows/ci.yml)

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v3

      - name: Set up Python
        uses: actions/setup-python@v4
        with:
          python-version: 3.9

      - name: Install dependencies
        run: |
          pip install -r requirements.txt
          pip install -r requirements-dev.txt

      - name: Run linting
        run: |
          flake8 src/ tests/
          mypy src/

      - name: Run tests
        run: |
          pytest --cov=src --cov-report=xml

      - name: Upload coverage
        uses: codecov/codecov-action@v3
        with:
          file: ./coverage.xml

  deploy:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
      - uses: actions/checkout@v3

      - name: Build Docker image
        run: docker build -t network-ai-service .

      - name: Deploy to production
        run: |
          # 배포 스크립트 실행
          ./scripts/deploy.sh
```

이 개발 가이드를 따라 일관성 있고 품질 높은 코드를 작성할 수 있습니다.
