# 🛡️ WindowsSentinel

Enterprise-grade Network Security Monitoring & DDoS Defense System

---

## 📁 프로젝트 구조

```
WS/
├── LogCheck/                          # 메인 애플리케이션
│   ├── Services/                      # 핵심 보안 서비스
│   │   ├── DDoSDetectionEngine.cs    # DDoS 탐지 엔진
│   │   ├── AutoBlockService.cs       # 자동 차단 시스템
│   │   ├── ToastNotificationService.cs
│   │   └── ...
│   ├── ViewModels/                    # MVVM 뷰모델
│   ├── Views/                         # UI 페이지
│   └── Models/                        # 데이터 모델
│
├── Demo_Scripts/                      # 🔥 데모 및 테스트 스크립트
│   ├── Simple_Attack_Generator.ps1   # TCP SYN Flood 생성기
│   ├── UDP_Flood_Generator.ps1       # UDP Flood 생성기
│   ├── 데모_완벽_가이드_실제트래픽버전.md
│   ├── 데모_테스트_가이드.md
│   └── README.md
│
├── docs/                              # 문서
├── AutoBlockTestApp/                  # 테스트 앱
└── network_ai_service/                # AI 서비스 (선택)
```

---

## 🚀 빠른 시작

### 1. 빌드 및 실행

```powershell
cd C:\My_Project\WS
dotnet build LogCheck/LogCheck.sln
cd LogCheck\bin\Debug\net8.0-windows
.\LogCheck.exe
```

### 2. 데모 테스트

```powershell
# 1. WindowsSentinel 실행
# 2. Setting → "데모 모드" 활성화
# 3. 관리자 권한 PowerShell에서:
cd C:\My_Project\WS\Demo_Scripts
.\Simple_Attack_Generator.ps1
```

📖 **자세한 가이드**: `Demo_Scripts/README.md` 참조

---

## ✨ 주요 기능

### 🔴 실시간 DDoS 탐지

- **7가지 공격 패턴** 탐지
  - SYN Flood
  - UDP Flood
  - HTTP Flood
  - Slowloris
  - DNS Amplification
  - Connection Frequency Attack
  - Bandwidth Spike Attack
- **10-20초 이내** 빠른 탐지
- **제로 False-Positive** 설계 (RFC1918 필터링)

### 🚫 자동 차단 시스템

- 공격 IP 즉시 차단
- Windows Firewall 규칙 자동 생성
- 화이트리스트 관리
- 차단 해제 기능

### 📊 실시간 대시보드

- LiveCharts 기반 시각화
- 24시간 위협 트렌드 분석
- 공격 유형별 통계
- 시스템 리소스 모니터링

### 🔔 Toast 알림 시스템

- 5가지 알림 타입 (Success/Warning/Error/Info/Security)
- 실시간 위협 알림
- 3-6초 자동 표시/숨김

### 📝 상세 로그 기록

- 모든 보안 이벤트 기록
- 타임스탬프, 소스 IP, 공격 유형
- 검색 및 필터링 기능
- 로그 내보내기

---

## 🎯 데모 모드

발표 및 테스트를 위한 **데모 모드** 제공:

### 활성화 방법

1. WindowsSentinel 실행
2. **Setting** 페이지로 이동
3. **"데모 모드 (사설 IP 공격 탐지 활성화)"** 체크

### 데모 모드 특징

- ✅ 로컬호스트(127.0.0.1) 탐지
- ✅ 사설 IP(RFC1918) 탐지
- ⚠️ 프로덕션 환경에서는 비활성화 필수

### 테스트 스크립트

```powershell
cd C:\My_Project\WS\Demo_Scripts

# 옵션 1: TCP SYN Flood
.\Simple_Attack_Generator.ps1

# 옵션 2: UDP Flood (더 강력)
.\UDP_Flood_Generator.ps1
```

---

## ⚙️ 기술 스택

- **.NET 8.0** (WPF)
- **LiveCharts** (차트 시각화)
- **WMI** (네트워크 모니터링)
- **Windows Firewall API** (자동 차단)
- **MVVM 패턴**

---

## 📋 시스템 요구사항

- **OS**: Windows 10/11 (64-bit)
- **Framework**: .NET 8.0 Runtime
- **권한**: 관리자 권한 (방화벽 규칙 생성)
- **메모리**: 최소 4GB RAM
- **디스크**: 100MB 여유 공간

---

## 🔧 DDoS 탐지 임계값

```csharp
MaxConnectionsPerSecond = 250;      // 초당 250+ 연결
MaxConnectionsPerMinute = 1500;     // 분당 1500+ 연결
MaxConcurrentConnections = 5000;    // 동시 5000+ 연결
MaxBytesPerSecond = 150MB;          // 초당 150MB+ 전송
SynFloodThreshold = 500;            // 초당 500+ SYN 패킷
UdpFloodThreshold = 1000;           // 초당 1000+ UDP 패킷
SlowLorisTimeout = 50;              // 50초+ 미완성 연결
HttpFloodThreshold = 500;           // 초당 500+ HTTP 요청
```

---

## 🎓 발표 준비

### 발표 일자

**2025년 10월 22일** (D-3일)

### 준비 자료

📖 **`Demo_Scripts/데모_완벽_가이드_실제트래픽버전.md`**

- 완벽한 데모 시연 가이드
- 발표 시나리오 및 타임라인
- 트러블슈팅 가이드
- 예상 질문 & 답변

### 체크리스트

- [ ] WindowsSentinel 빌드 테스트
- [ ] 데모 모드 기능 확인
- [ ] 공격 스크립트 테스트 (최소 3회)
- [ ] Toast 알림 작동 확인
- [ ] 화면 녹화 준비 (백업용)

---

## 📞 문의 및 지원

- **Repository**: https://github.com/gkwp1216/WindowsSentinel
- **Issues**: GitHub Issues 페이지
- **Developer**: gkwp1216

---

## 📄 라이선스

[라이선스 정보 추가 예정]

---

## 🏆 프로젝트 하이라이트

✨ **실시간 탐지**: 10-20초 이내 DDoS 공격 탐지  
✨ **자동 대응**: 관리자 개입 없이 자동 차단  
✨ **제로 오탐**: RFC1918 필터링으로 false-positive 최소화  
✨ **직관적 UI**: 실시간 대시보드와 Toast 알림  
✨ **데모 모드**: 발표 및 테스트를 위한 특별 모드

---

**마지막 업데이트**: 2025년 10월 19일  
**버전**: 1.0.0
