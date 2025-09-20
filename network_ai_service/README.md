# Network AI Service

Python 기반 실시간 네트워크 이상 탐지 AI 서비스로, WindowsSentinel 프로젝트와 통합되어 네트워크 보안을 강화합니다.

## ✨ 주요 기능

- **🔍 실시간 네트워크 모니터링**: 시스템의 모든 네트워크 연결을 실시간으로 수집 및 분석
- **🧠 AI 기반 이상 탐지**: 머신러닝 모델을 통한 정상 패턴 학습 및 이상 행동 탐지
- **⚡ 고성능 API**: FastAPI 기반의 고속 REST API 및 WebSocket 지원
- **📊 실시간 모니터링**: Prometheus/Grafana 기반의 실시간 성능 모니터링
- **🔧 자동 재학습**: 시간에 따른 패턴 변화에 자동 적응하는 모델 재학습 시스템

## 🏗️ 아키텍처

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Data          │    │   ML Pipeline    │    │   API Gateway   │
│   Collection    │ -> │   & Models       │ -> │   & WebSocket   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                        │                       │
         ▼                        ▼                       ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   SQLite DB     │    │   Model Store    │    │   C# WPF App    │
│   Time Series   │    │   Trained Models │    │   Integration   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 🚀 빠른 시작

### Docker를 이용한 실행 (권장)

```bash
cd network_ai_service
docker-compose up -d
```

### 수동 설치 및 실행

```bash
# 가상환경 생성 및 활성화
python -m venv venv
venv\Scripts\activate  # Windows
# source venv/bin/activate  # Linux/Mac

# 의존성 설치
pip install -r requirements.txt

# 데이터베이스 초기화
python scripts/init_database.py

# 서비스 시작
uvicorn src.api.main:app --host 0.0.0.0 --port 8000
```

### 접속 확인

- **API 문서**: http://localhost:8000/docs
- **헬스 체크**: http://localhost:8000/api/v1/health
- **모니터링**: http://localhost:3000 (Grafana)

## 📊 사용 예제

### Python 클라이언트

```python
import aiohttp
import asyncio

async def analyze_connection():
    data = {
        "process_id": 1234,
        "process_name": "chrome.exe",
        "local_ip": "192.168.1.100",
        "remote_ip": "203.0.113.1",
        "protocol": "TCP",
        "bytes_sent": 1024,
        "bytes_received": 4096
    }

    async with aiohttp.ClientSession() as session:
        async with session.post(
            "http://localhost:8000/api/v1/analyze",
            json=data,
            headers={"Authorization": "Bearer YOUR_TOKEN"}
        ) as response:
            result = await response.json()
            print(f"이상 탐지 결과: {result['result']['is_anomaly']}")
            print(f"이상 점수: {result['result']['anomaly_score']}")

asyncio.run(analyze_connection())
```

### C# 클라이언트 (WindowsSentinel 통합)

```csharp
public class NetworkAIClient
{
    private readonly HttpClient _httpClient;

    public async Task<AnalysisResult> AnalyzeConnectionAsync(NetworkConnection connection)
    {
        var request = new
        {
            process_id = connection.ProcessId,
            process_name = connection.ProcessName,
            local_ip = connection.LocalIP,
            remote_ip = connection.RemoteIP,
            protocol = connection.Protocol,
            bytes_sent = connection.BytesSent,
            bytes_received = connection.BytesReceived
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/analyze", request);
        return await response.Content.ReadFromJsonAsync<AnalysisResult>();
    }
}
```

## 📈 성능 지표

### 예상 성능

- **처리 지연시간**: < 10ms (95th percentile)
- **처리량**: 1,000+ 연결/초
- **메모리 사용량**: < 512MB
- **모델 정확도**: > 95%
- **False Positive Rate**: < 2%

### 확장성

- **수평 확장**: Docker Swarm/Kubernetes 지원
- **데이터 분할**: 시간 기반 데이터 파티셔닝
- **로드 밸런싱**: 여러 인스턴스 간 요청 분산
- **캐싱**: Redis 기반 결과 캐싱

## 🔐 보안 기능

- **API 인증**: JWT 토큰 기반 인증
- **데이터 암호화**: 저장 데이터 AES-256 암호화
- **네트워크 보안**: TLS 1.3 통신 암호화
- **접근 로깅**: 모든 API 접근 기록
- **개인정보 보호**: IP 주소 해싱 처리

## 📚 문서

- [📖 전체 가이드](CLAUDE.md) - 프로젝트 개요 및 시작 가이드
- [🏗️ 아키텍처 설계](docs/architecture.md) - 시스템 아키텍처 상세 설명
- [📋 API 문서](docs/api-reference.md) - REST API 명세서
- [🛠️ 개발 가이드](docs/development.md) - 개발 환경 설정 및 컨벤션
- [🚀 배포 가이드](docs/deployment.md) - 프로덕션 배포 방법

## 🤝 WindowsSentinel 통합

이 서비스는 WindowsSentinel 프로젝트와 완벽하게 통합되도록 설계되었습니다:

### 통합 방법

1. **데이터 수집**: 기존 `ProcessNetworkMapper` 대신 Python 서비스 사용
2. **실시간 분석**: REST API 또는 WebSocket을 통한 실시간 이상 탐지
3. **UI 통합**: WPF 애플리케이션에서 분석 결과 표시
4. **알림 시스템**: 이상 탐지 시 즉시 알림 전송

### 마이그레이션 계획

- **Phase 1**: Python 서비스 구축 및 기본 연동
- **Phase 2**: 고급 ML 모델 적용 및 성능 최적화
- **Phase 3**: 완전한 대체 및 확장 기능 추가

## 🛠️ 개발 상태

- [x] **프로젝트 구조 설계**
- [x] **아키텍처 문서 작성**
- [x] **API 명세 정의**
- [x] **개발 가이드라인 수립**
- [x] **배포 전략 수립**
- [ ] **핵심 모듈 구현**
- [ ] **ML 파이프라인 구현**
- [ ] **테스트 코드 작성**
- [ ] **성능 최적화**
- [ ] **프로덕션 배포**

## 📞 문의 및 지원

- **이슈 리포트**: GitHub Issues
- **기능 요청**: GitHub Discussions
- **보안 이슈**: security@yourcompany.com

---

**🌟 WindowsSentinel의 네트워크 보안을 한 단계 업그레이드하세요!**
