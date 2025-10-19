# 📁 WindowsSentinel 프로젝트 구조

## 🗂️ 전체 디렉토리 구조

```
C:\My_Project\WS/
│
├── 📄 README.md                           # 프로젝트 개요 및 시작 가이드
├── 📄 App.xaml                            # WPF 애플리케이션 진입점
├── 📄 App.xaml.cs
│
├── 📁 LogCheck/                           # 🔥 메인 애플리케이션
│   ├── 📄 LogCheck.sln                    # 솔루션 파일
│   ├── 📄 LogCheck.csproj                 # 프로젝트 파일
│   ├── 📄 MainWindows.xaml                # 메인 윈도우
│   │
│   ├── 📁 Services/                       # 🔥 핵심 비즈니스 로직
│   │   ├── DDoSDetectionEngine.cs        # DDoS 탐지 엔진 ⭐
│   │   ├── IntegratedDDoSDefenseSystem.cs # 통합 방어 시스템
│   │   ├── AutoBlockService.cs           # 자동 차단 서비스
│   │   ├── ToastNotificationService.cs   # Toast 알림
│   │   ├── SecurityEventLogger.cs        # 보안 이벤트 로거
│   │   ├── RealTimeSecurityAnalyzer.cs   # 실시간 분석기
│   │   └── ...
│   │
│   ├── 📁 ViewModels/                     # MVVM 뷰모델
│   │   ├── SecurityDashboardViewModel.cs # 대시보드 VM ⭐
│   │   ├── AutoBlockViewModel.cs         # 자동차단 VM
│   │   ├── NetworkMonitorViewModel.cs    # 네트워크 모니터 VM
│   │   └── ...
│   │
│   ├── 📁 Views/                          # UI 페이지 (XAML)
│   │   ├── SecurityDashboard.xaml        # 보안 대시보드
│   │   ├── AutoBlock.xaml                # 자동 차단
│   │   ├── NetWorks_New.xaml             # 네트워크 모니터
│   │   ├── Logs.xaml                     # 로그
│   │   ├── ThreatIntelligence.xaml       # 위협 인텔리전스
│   │   ├── Vaccine.xaml                  # 백신
│   │   ├── Recoverys.xaml                # 복구
│   │   ├── ProgramsList.xaml             # 프로그램 목록
│   │   └── Setting.xaml                  # 설정 (데모 모드 토글)
│   │
│   ├── 📁 Models/                         # 데이터 모델
│   │   ├── ProcessNetworkInfo.cs         # 네트워크 연결 정보
│   │   ├── DDoSAlert.cs                  # DDoS 경고
│   │   ├── BlockedIPInfo.cs              # 차단 IP 정보
│   │   └── ...
│   │
│   ├── 📁 Converters/                     # XAML 값 변환기
│   ├── 📁 Controls/                       # 커스텀 컨트롤
│   ├── 📁 Resources/                      # 리소스 파일
│   └── 📁 bin/Debug/net8.0-windows/       # 빌드 출력
│
├── 📁 Demo_Scripts/                       # 🔥 데모 및 테스트 스크립트
│   ├── 📄 README.md                       # 스크립트 사용 가이드
│   ├── 📄 Simple_Attack_Generator.ps1    # TCP SYN Flood 생성기 ⭐
│   ├── 📄 UDP_Flood_Generator.ps1        # UDP Flood 생성기 ⭐
│   ├── 📄 데모_완벽_가이드_실제트래픽버전.md  # 완벽한 데모 가이드 ⭐
│   └── 📄 데모_테스트_가이드.md            # 간단 테스트 가이드
│
├── 📁 docs/                               # 문서
│   └── monitoring-architecture.md         # 모니터링 아키텍처
│
├── 📁 AutoBlockTestApp/                   # 자동 차단 테스트 앱
│   ├── AutoBlockTestApp.csproj
│   └── Program.cs
│
├── 📁 network_ai_service/                 # AI 서비스 (선택)
│
└── 📁 Old_tools/                          # 레거시 코드
```

---

## 🔥 핵심 파일 설명

### 1. **DDoSDetectionEngine.cs** ⭐⭐⭐

**위치**: `LogCheck/Services/DDoSDetectionEngine.cs`

**역할**: DDoS 공격 탐지의 핵심 엔진

**주요 기능**:

- 7가지 공격 패턴 탐지 (SYN Flood, UDP Flood, HTTP Flood 등)
- RFC1918 사설 IP 필터링 (프로덕션 모드)
- **DemoMode 플래그**: 데모 시 127.0.0.1 탐지 가능

**핵심 코드**:

```csharp
public static bool DemoMode { get; set; } = false;

var filteredConnections = connections
    .Where(conn => DemoMode || !IsPrivateIP(conn.RemoteAddress))
    .ToList();
```

---

### 2. **Setting.xaml + Setting.xaml.cs** ⭐⭐

**위치**: `LogCheck/Setting.xaml`, `LogCheck/Setting.xaml.cs`

**역할**: 설정 페이지 (데모 모드 토글 포함)

**데모 모드 체크박스**:

```xml
<CheckBox x:Name="DemoModeCheckBox"
          Content="데모 모드 (사설 IP 공격 탐지 활성화)"
          Checked="DemoModeCheckBox_Changed"
          Unchecked="DemoModeCheckBox_Changed" />
```

**이벤트 핸들러**:

```csharp
private void DemoModeCheckBox_Changed(object sender, RoutedEventArgs e)
{
    var isChecked = (sender as CheckBox)?.IsChecked == true;
    DDoSDetectionEngine.DemoMode = isChecked;
}
```

---

### 3. **SecurityDashboardViewModel.cs** ⭐⭐

**위치**: `LogCheck/ViewModels/SecurityDashboardViewModel.cs`

**역할**: 실시간 보안 대시보드 데이터 관리

**주요 기능**:

- 24시간 위협 트렌드 차트 업데이트
- 실시간 보안 이벤트 생성
- Toast 알림 연동

---

### 4. **ToastNotificationService.cs** ⭐

**위치**: `LogCheck/Services/ToastNotificationService.cs`

**역할**: 실시간 Toast 알림 표시

**알림 타입**:

- Success (녹색)
- Warning (노란색)
- Error (빨간색)
- Info (파란색)
- Security (빨간색 - DDoS 경고)

---

### 5. **Simple_Attack_Generator.ps1** ⭐⭐⭐

**위치**: `Demo_Scripts/Simple_Attack_Generator.ps1`

**역할**: TCP SYN Flood 공격 시뮬레이션

**사용법**:

```powershell
cd C:\My_Project\WS\Demo_Scripts
.\Simple_Attack_Generator.ps1
```

**동작**:

- 127.0.0.1에 실제 TCP 연결 시도
- 100 packets/sec (조절 가능)
- 30초간 지속

---

### 6. **UDP_Flood_Generator.ps1** ⭐⭐

**위치**: `Demo_Scripts/UDP_Flood_Generator.ps1`

**역할**: UDP Flood 공격 시뮬레이션

**특징**:

- 더 강력한 공격 (200 packets/sec)
- DNS, NTP, SNMP 포트 타겟
- 512 bytes 페이로드

---

## 📊 데이터 흐름

```
[네트워크 트래픽]
        ↓
[WMI/Netstat 수집] → NetWorks_New.xaml.cs
        ↓
[DDoSDetectionEngine] ← DemoMode 플래그 확인
        ↓
[패턴 분석 & 필터링]
        ↓
    [DDoS 탐지?]
        ↓
   ┌─────┴─────┐
   ↓           ↓
[AutoBlock]  [Toast 알림]
   ↓           ↓
[Firewall]  [Dashboard]
   ↓           ↓
[Logs]      [사용자]
```

---

## 🎯 데모 시연 흐름

```
1. [WindowsSentinel 실행]
   LogCheck/bin/Debug/net8.0-windows/LogCheck.exe

2. [설정 페이지]
   Setting → "데모 모드" 체크박스 활성화
   → DDoSDetectionEngine.DemoMode = true

3. [공격 스크립트 실행]
   Demo_Scripts/Simple_Attack_Generator.ps1
   → 127.0.0.1로 실제 TCP 패킷 전송

4. [탐지 프로세스]
   NetWorks_New (WMI 수집)
   → DDoSDetectionEngine (패턴 분석)
   → DemoMode = true이므로 127.0.0.1 탐지
   → DDoS 경고 발생

5. [자동 대응]
   → AutoBlockService (IP 차단)
   → ToastNotificationService (알림)
   → SecurityDashboard (차트 업데이트)
   → Logs (이벤트 기록)
```

---

## 🔧 주요 설정 파일

### LogCheck.csproj

- 프로젝트 종속성
- NuGet 패키지 (LiveCharts, System.Management)
- 빌드 설정

### App.xaml

- 전역 리소스
- 테마 (LightTheme.xaml, DarkTheme.xaml)

### simulation-config.json (AS 설정)

- Attack_Simulator 설정
- TargetIPRange: 127.0.0.1/32

---

## 📝 빌드 및 실행

### 빌드

```powershell
cd C:\My_Project\WS
dotnet build LogCheck/LogCheck.sln
```

### 실행

```powershell
cd LogCheck/bin/Debug/net8.0-windows
.\LogCheck.exe
```

### 데모 테스트

```powershell
# 1. WindowsSentinel 실행 후 데모 모드 활성화
# 2. 관리자 PowerShell:
cd C:\My_Project\WS\Demo_Scripts
.\Simple_Attack_Generator.ps1
```

---

## 🎓 학습 경로

### 초급: 기본 이해

1. `README.md` - 프로젝트 개요
2. `Demo_Scripts/README.md` - 데모 스크립트 사용법
3. `Demo_Scripts/데모_테스트_가이드.md` - 간단한 테스트

### 중급: 코드 분석

1. `DDoSDetectionEngine.cs` - 탐지 알고리즘
2. `AutoBlockService.cs` - 차단 메커니즘
3. `SecurityDashboardViewModel.cs` - 데이터 바인딩

### 고급: 아키텍처

1. `docs/monitoring-architecture.md` - 전체 구조
2. `IntegratedDDoSDefenseSystem.cs` - 통합 시스템
3. `Demo_Scripts/데모_완벽_가이드_실제트래픽버전.md` - 완벽 가이드

---

## 📞 문의

**Repository**: https://github.com/gkwp1216/WindowsSentinel  
**Issues**: GitHub Issues  
**Developer**: gkwp1216

---

**마지막 업데이트**: 2025년 10월 19일
