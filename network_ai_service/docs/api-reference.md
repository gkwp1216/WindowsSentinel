# Network AI Service - API Reference

## 📋 API 개요

Network AI Service는 RESTful API와 WebSocket을 통해 실시간 네트워크 이상 탐지 서비스를 제공합니다.

**Base URL**: `http://localhost:8000/api/v1`  
**WebSocket URL**: `ws://localhost:8000/ws`

## 🔐 인증

모든 API 요청은 JWT 토큰을 통한 인증이 필요합니다.

### 토큰 발급

```http
POST /api/v1/auth/token
Content-Type: application/json

{
    "username": "api_user",
    "password": "secure_password"
}
```

**Response:**

```json
{
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
  "token_type": "bearer",
  "expires_in": 3600
}
```

### 토큰 사용

```http
Authorization: Bearer {access_token}
```

## 🔍 네트워크 분석 API

### 단일 네트워크 데이터 분석

실시간으로 네트워크 데이터를 분석하여 이상 여부를 판단합니다.

```http
POST /api/v1/analyze
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body:**

```json
{
  "timestamp": "2025-09-18T14:30:00Z",
  "process_id": 1234,
  "process_name": "chrome.exe",
  "local_ip": "192.168.1.100",
  "local_port": 54321,
  "remote_ip": "203.0.113.1",
  "remote_port": 443,
  "protocol": "TCP",
  "bytes_sent": 1024,
  "bytes_received": 4096,
  "connection_state": "ESTABLISHED",
  "duration_seconds": 30.5
}
```

**Response:**

```json
{
  "request_id": "req_12345",
  "timestamp": "2025-09-18T14:30:01Z",
  "result": {
    "is_anomaly": false,
    "anomaly_score": 0.15,
    "confidence": 0.92,
    "risk_level": "LOW"
  },
  "model_info": {
    "model_name": "ensemble_v1.2",
    "version": "1.2.0",
    "trained_at": "2025-09-15T10:00:00Z"
  },
  "explanation": {
    "factors": [
      {
        "feature": "bytes_per_second",
        "value": 136.5,
        "impact": 0.1,
        "description": "Normal data transfer rate"
      }
    ],
    "similar_patterns": 15,
    "anomaly_threshold": 0.75
  },
  "processing_time_ms": 8
}
```

### 배치 네트워크 데이터 분석

여러 네트워크 연결을 한 번에 분석합니다.

```http
POST /api/v1/analyze/batch
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body:**

```json
{
  "batch_id": "batch_001",
  "connections": [
    {
      "timestamp": "2025-09-18T14:30:00Z",
      "process_id": 1234
      // ... 네트워크 데이터
    },
    {
      "timestamp": "2025-09-18T14:30:01Z",
      "process_id": 5678
      // ... 네트워크 데이터
    }
  ]
}
```

**Response:**

```json
{
  "batch_id": "batch_001",
  "processed_at": "2025-09-18T14:30:02Z",
  "total_connections": 2,
  "anomalies_detected": 0,
  "results": [
    {
      "connection_index": 0,
      "is_anomaly": false,
      "anomaly_score": 0.15
      // ... 개별 결과
    }
  ],
  "summary": {
    "normal_count": 2,
    "anomaly_count": 0,
    "high_risk_count": 0,
    "average_confidence": 0.89
  }
}
```

## 🧠 모델 관리 API

### 모델 상태 조회

현재 로드된 모델의 상태와 성능을 확인합니다.

```http
GET /api/v1/models/status
Authorization: Bearer {token}
```

**Response:**

```json
{
  "models": [
    {
      "name": "isolation_forest",
      "version": "1.2.0",
      "status": "active",
      "trained_at": "2025-09-15T10:00:00Z",
      "accuracy": 0.94,
      "precision": 0.91,
      "recall": 0.89,
      "f1_score": 0.9,
      "last_updated": "2025-09-18T14:00:00Z"
    },
    {
      "name": "ensemble_v1.2",
      "version": "1.2.0",
      "status": "active",
      "models_count": 3,
      "performance": {
        "accuracy": 0.96,
        "false_positive_rate": 0.02
      }
    }
  ],
  "default_model": "ensemble_v1.2",
  "total_predictions_today": 15420
}
```

### 모델 학습 시작

새로운 모델 학습을 시작합니다.

```http
POST /api/v1/models/train
Content-Type: application/json
Authorization: Bearer {token}
```

**Request Body:**

```json
{
  "training_config": {
    "model_type": "ensemble",
    "data_source": "last_30_days",
    "validation_split": 0.2,
    "hyperparameters": {
      "n_estimators": 100,
      "contamination": 0.05
    }
  },
  "training_name": "weekly_retrain_001",
  "notify_on_completion": true
}
```

**Response:**

```json
{
  "training_job_id": "job_789",
  "status": "started",
  "estimated_duration_minutes": 45,
  "started_at": "2025-09-18T14:30:00Z",
  "progress_url": "/api/v1/training/job_789/progress"
}
```

### 학습 진행 상태 조회

```http
GET /api/v1/training/{job_id}/progress
Authorization: Bearer {token}
```

**Response:**

```json
{
  "job_id": "job_789",
  "status": "training",
  "progress_percent": 65,
  "current_step": "model_validation",
  "steps_completed": 4,
  "total_steps": 6,
  "elapsed_time_minutes": 28,
  "estimated_remaining_minutes": 12,
  "logs": [
    "Data preprocessing completed",
    "Feature engineering completed",
    "Model training started",
    "Cross-validation in progress..."
  ]
}
```

## 📊 통계 및 메트릭 API

### 실시간 통계 조회

```http
GET /api/v1/stats/realtime
Authorization: Bearer {token}
```

**Response:**

```json
{
  "current_stats": {
    "active_connections": 156,
    "predictions_per_minute": 45,
    "anomalies_detected_today": 3,
    "system_load": {
      "cpu_usage_percent": 15.2,
      "memory_usage_mb": 245,
      "prediction_latency_ms": 8.5
    }
  },
  "model_performance": {
    "accuracy_today": 0.94,
    "false_positive_rate": 0.02,
    "predictions_count": 15420
  },
  "top_anomalies": [
    {
      "timestamp": "2025-09-18T14:25:00Z",
      "process": "unknown.exe",
      "anomaly_score": 0.95,
      "risk_level": "HIGH"
    }
  ]
}
```

### 기간별 통계 조회

```http
GET /api/v1/stats/historical?period=7d&granularity=1h
Authorization: Bearer {token}
```

**Response:**

```json
{
  "period": "7d",
  "granularity": "1h",
  "data_points": [
    {
      "timestamp": "2025-09-18T14:00:00Z",
      "total_predictions": 156,
      "anomalies_detected": 2,
      "average_anomaly_score": 0.23,
      "model_accuracy": 0.94
    }
    // ... 더 많은 데이터 포인트
  ],
  "summary": {
    "total_predictions": 105840,
    "total_anomalies": 45,
    "anomaly_rate": 0.000425,
    "average_accuracy": 0.943
  }
}
```

## 🔧 시스템 관리 API

### 헬스 체크

```http
GET /api/v1/health
```

**Response:**

```json
{
  "status": "healthy",
  "timestamp": "2025-09-18T14:30:00Z",
  "version": "1.0.0",
  "components": {
    "database": {
      "status": "healthy",
      "response_time_ms": 2
    },
    "models": {
      "status": "healthy",
      "loaded_models": 3
    },
    "data_collector": {
      "status": "healthy",
      "collections_per_minute": 60
    }
  },
  "system_info": {
    "uptime_seconds": 86400,
    "memory_usage_mb": 245,
    "cpu_usage_percent": 15.2
  }
}
```

### 설정 조회 및 변경

```http
GET /api/v1/config
Authorization: Bearer {token}
```

```http
PUT /api/v1/config
Content-Type: application/json
Authorization: Bearer {token}

{
    "collection_interval_seconds": 1.0,
    "anomaly_threshold": 0.75,
    "max_connections_per_process": 100,
    "alert_config": {
        "enable_email_alerts": true,
        "enable_webhook_alerts": true,
        "webhook_url": "https://hooks.slack.com/..."
    }
}
```

## 🌐 WebSocket API

### 실시간 분석 결과 스트리밍

```javascript
const ws = new WebSocket("ws://localhost:8000/ws/realtime?token={jwt_token}");

ws.onmessage = function (event) {
  const data = JSON.parse(event.data);
  console.log("Received:", data);
};

// 구독 요청
ws.send(
  JSON.stringify({
    type: "subscribe",
    channels: ["anomalies", "stats", "alerts"],
  })
);
```

**WebSocket 메시지 형식:**

```json
{
  "type": "anomaly_detected",
  "timestamp": "2025-09-18T14:30:00Z",
  "data": {
    "anomaly_score": 0.89,
    "process_name": "suspicious.exe",
    "remote_ip": "203.0.113.50",
    "risk_level": "HIGH"
  }
}
```

## 📝 에러 코드

| 상태 코드 | 에러 코드             | 설명                     |
| --------- | --------------------- | ------------------------ |
| 400       | `INVALID_REQUEST`     | 잘못된 요청 형식         |
| 401       | `UNAUTHORIZED`        | 인증 토큰 누락 또는 무효 |
| 403       | `FORBIDDEN`           | 권한 부족                |
| 404       | `NOT_FOUND`           | 리소스를 찾을 수 없음    |
| 429       | `RATE_LIMIT_EXCEEDED` | 요청 한도 초과           |
| 500       | `INTERNAL_ERROR`      | 서버 내부 오류           |
| 503       | `SERVICE_UNAVAILABLE` | 서비스 일시 중단         |

**에러 응답 형식:**

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "Missing required field: process_id",
    "details": {
      "field": "process_id",
      "expected_type": "integer"
    },
    "request_id": "req_12345",
    "timestamp": "2025-09-18T14:30:00Z"
  }
}
```

## 🚀 사용 예제

### C# 클라이언트 예제

```csharp
public class NetworkAIClient
{
    private readonly HttpClient _httpClient;

    public async Task<AnalysisResult> AnalyzeConnectionAsync(NetworkConnection connection)
    {
        var request = new
        {
            timestamp = connection.Timestamp,
            process_id = connection.ProcessId,
            process_name = connection.ProcessName,
            // ... 기타 필드
        };

        var response = await _httpClient.PostAsJsonAsync("/api/v1/analyze", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AnalysisResult>();
    }
}
```

### Python 클라이언트 예제

```python
import aiohttp
import asyncio

class NetworkAIClient:
    def __init__(self, base_url: str, token: str):
        self.base_url = base_url
        self.headers = {"Authorization": f"Bearer {token}"}

    async def analyze_connection(self, connection_data: dict):
        async with aiohttp.ClientSession() as session:
            async with session.post(
                f"{self.base_url}/api/v1/analyze",
                json=connection_data,
                headers=self.headers
            ) as response:
                return await response.json()
```

이 API는 WindowsSentinel과의 원활한 통합을 위해 설계되었으며, 확장 가능하고 사용하기 쉬운 인터페이스를 제공합니다.
