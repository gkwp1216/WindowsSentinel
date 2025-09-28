# 자동 차단 시스템 구현 규칙 (Auto-Block System Rules)

## 📋 개요

Windows Sentinel의 자동 차단 시스템은 실시간으로 네트워크 위협을 탐지하고 자동으로 차단하는 3단계 보안 시스템입니다. 각 단계는 위험도와 확신도에 따라 다른 대응 전략을 사용합니다.

---

## 🔴 1단계: 즉시 차단 (Critical Block) - 자동 실행

### 적용 대상

- **확실한 악성 행위**: 100% 확신할 수 있는 위험 패턴
- **시스템 보안에 치명적 위협**: 즉각적인 대응이 필요한 경우

### 차단 규칙

#### 1.1 알려진 악성 IP/도메인

```csharp
// 예시 구현
private static readonly HashSet<string> MaliciousIPs = new()
{
    "192.168.1.100", // 예시 악성 IP
    "malicious-domain.com",
    "phishing-site.net"
};

// C&C 서버 패턴
private static readonly string[] CnCPatterns =
{
    @".*\.tk$",           // 무료 도메인 (.tk)
    @".*\.ml$",           // 무료 도메인 (.ml)
    @".*\.ga$",           // 무료 도메인 (.ga)
    @"\d+\.\d+\.\d+\.\d+:\d{4,5}$" // 직접 IP:Port 연결
};
```

#### 1.2 의심스러운 포트 사용

```csharp
// 일반적으로 악성코드가 사용하는 포트
private static readonly int[] SuspiciousPorts =
{
    1337, 31337, 12345, 54321, 9999, 4444, 5555, // 해커 툴 포트
    6666, 6667, 6668, 6669,  // IRC 봇넷
    1234, 2222, 3333, 7777,  // 백도어 포트
    9090, 8080, 3389         // 비정상적 원격 접속
};

// 정상 포트 화이트리스트
private static readonly int[] Legitimateports =
{
    80, 443, 53, 25, 110, 143, 993, 995, // 웹, DNS, 메일
    21, 22, 23, 3389,                     // FTP, SSH, Telnet, RDP
    445, 139, 135, 137                    // SMB, NetBIOS
};
```

#### 1.3 System Idle Process 위장 탐지

```csharp
// System Idle Process 위장 패턴 탐지
private bool IsSystemIdleProcessForgery(ProcessNetworkInfo process)
{
    var processName = process.ProcessName?.Trim();

    // 정상 System Idle Process 특징
    if (processName == "System Idle Process")
    {
        // PID 0이 아니면 위조
        if (process.ProcessId != 0) return true;

        // .exe 확장자가 있으면 위조
        if (process.ProcessPath?.EndsWith(".exe") == true) return true;

        // 네트워크 연결이 있으면 위조 (System Idle Process는 네트워크 사용 안함)
        if (process.LocalPort > 0 || process.RemotePort > 0) return true;
    }

    // 유사한 이름 패턴 탐지
    var suspiciousNames = new[]
    {
        "System ldle Process",    // l(소문자 L) 대신 I
        "System Idle Process.exe", // .exe 확장자
        "System  Idle Process",   // 공백 2개
        "System Idle  Process",   // 공백 2개
        "Systern Idle Process",   // n 대신 rn
        "Sys tem Idle Process"    // 공백 삽입
    };

    return suspiciousNames.Any(name =>
        string.Equals(processName, name, StringComparison.OrdinalIgnoreCase));
}
```

#### 1.4 대용량 데이터 전송

```csharp
// 비정상적 대용량 전송 탐지
private bool IsAbnormalDataTransfer(ProcessNetworkInfo process)
{
    // 10MB 이상 단시간 전송
    if (process.DataTransferred > 10 * 1024 * 1024 &&
        process.ConnectionDuration < TimeSpan.FromMinutes(1))
    {
        return true;
    }

    // 100MB 이상 지속적 전송 (백그라운드에서)
    if (process.DataTransferred > 100 * 1024 * 1024 &&
        process.ConnectionStartTime < DateTime.Now.AddHours(-1))
    {
        return true;
    }

    return false;
}
```

### 차단 동작

1. **즉시 연결 종료**: TCP RST 패킷 전송
2. **프로세스 강제 종료**: 악성 프로세스 terminat
3. **방화벽 규칙 추가**: Windows Firewall에 차단 규칙 등록
4. **사용자 알림**: 긴급 알림 팝업 표시
5. **로그 기록**: 상세 차단 이력 저장

---

## 🟡 2단계: 경고 후 차단 (Warning Block) - 사용자 확인 후 실행

### 적용 대상

- **의심스러운 행위**: 70-90% 확신도의 위험 패턴
- **잠재적 위험**: 정상 프로그램일 가능성도 있지만 주의가 필요한 경우

### 차단 규칙

#### 2.1 비정상적 네트워크 패턴

```csharp
// 의심스러운 네트워크 행동 패턴
private bool IsSuspiciousNetworkBehavior(ProcessNetworkInfo process)
{
    // 짧은 시간 내 다수 연결 시도
    var recentConnections = GetRecentConnectionsByProcess(process.ProcessId, TimeSpan.FromMinutes(5));
    if (recentConnections.Count > 20)
    {
        return true;
    }

    // 다수의 다른 IP로 동시 연결
    var uniqueIPs = recentConnections.Select(c => c.RemoteAddress).Distinct().Count();
    if (uniqueIPs > 10)
    {
        return true;
    }

    // 비정상적 시간대 활동 (새벽 2-6시)
    var currentHour = DateTime.Now.Hour;
    if (currentHour >= 2 && currentHour <= 6 && process.DataTransferred > 1024 * 1024)
    {
        return true;
    }

    return false;
}
```

#### 2.2 알려지지 않은 프로세스

```csharp
// 서명되지 않은 또는 알려지지 않은 프로세스
private bool IsUnknownProcess(ProcessNetworkInfo process)
{
    // 디지털 서명 검증
    if (!HasValidDigitalSignature(process.ProcessPath))
    {
        return true;
    }

    // 시스템 폴더 외부의 시스템 유사 이름
    var systemLikeNames = new[] { "svchost", "winlogon", "explorer", "lsass" };
    var processName = Path.GetFileNameWithoutExtension(process.ProcessName);

    if (systemLikeNames.Any(name => processName?.StartsWith(name, StringComparison.OrdinalIgnoreCase) == true))
    {
        var systemPaths = new[] { @"C:\Windows\System32", @"C:\Windows\SysWOW64" };
        if (!systemPaths.Any(path => process.ProcessPath?.StartsWith(path, StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }
    }

    return false;
}
```

#### 2.3 외부 국가 IP 연결

```csharp
// 특정 국가 IP 대역 체크 (예: 중국, 러시아, 북한 등)
private static readonly Dictionary<string, string[]> RestrictedCountryIPs = new()
{
    ["China"] = new[]
    {
        "1.0.1.0/24", "1.0.2.0/24", "1.0.8.0/24", // 중국 IP 대역 예시
        // 실제로는 더 정확한 GeoIP 데이터베이스 필요
    },
    ["Russia"] = new[]
    {
        "5.8.0.0/16", "5.9.0.0/16", // 러시아 IP 대역 예시
    }
};

private bool IsConnectionToRestrictedCountry(string remoteIP)
{
    // GeoIP 라이브러리를 사용하여 실제 국가 확인
    // 여기서는 단순 예시만 제공
    foreach (var country in RestrictedCountryIPs)
    {
        foreach (var ipRange in country.Value)
        {
            if (IsIPInRange(remoteIP, ipRange))
            {
                return true;
            }
        }
    }
    return false;
}
```

### 차단 동작

1. **경고 팝업 표시**: 10초 카운트다운과 함께 사용자 선택 요구
2. **사용자 옵션 제공**:
   - 즉시 차단
   - 일시적 허용 (1시간)
   - 영구 허용 (화이트리스트 추가)
   - 더 자세한 정보 보기
3. **기본 동작**: 10초 후 자동 차단 (안전 우선)

---

## 🟢 3단계: 모니터링 강화 (Enhanced Monitoring) - 관찰 및 기록

### 적용 대상

- **경미한 의심 활동**: 30-70% 확신도의 패턴
- **정상적일 가능성 높음**: 하지만 지속적 관찰이 필요한 경우

### 모니터링 규칙

#### 3.1 새로운 프로그램의 네트워크 활동

```csharp
// 최근 설치된 프로그램의 첫 네트워크 활동
private bool IsNewProgramNetworkActivity(ProcessNetworkInfo process)
{
    var installDate = GetProgramInstallDate(process.ProcessPath);
    if (installDate.HasValue && installDate.Value > DateTime.Now.AddDays(-7))
    {
        // 1주일 내 설치된 프로그램
        return true;
    }

    // 처음 보는 프로세스 경로
    if (!IsKnownProcess(process.ProcessPath))
    {
        return true;
    }

    return false;
}
```

#### 3.2 비표준 포트 사용

```csharp
// 일반적이지 않은 포트 사용
private bool IsNonStandardPort(int port)
{
    var commonPorts = new[] { 80, 443, 53, 25, 110, 143, 21, 22, 23 };
    var gamesPorts = new[] { 27015, 7777, 25565, 19132 }; // Steam, Minecraft 등
    var p2pPorts = new[] { 6881, 6882, 6883, 6884, 6885 }; // BitTorrent

    return !commonPorts.Contains(port) &&
           !gamesPorts.Contains(port) &&
           !p2pPorts.Contains(port) &&
           port > 1024; // 시스템 포트 제외
}
```

#### 3.3 주기적 통신 패턴

```csharp
// 정규적인 간격의 통신 (봇넷 의심)
private bool HasPeriodicCommunication(ProcessNetworkInfo process)
{
    var connections = GetConnectionHistory(process.ProcessId, TimeSpan.FromHours(1));

    // 5분 간격으로 정확히 통신하는 패턴
    var intervals = connections
        .OrderBy(c => c.ConnectionStartTime)
        .Skip(1)
        .Select((c, i) => (c.ConnectionStartTime - connections[i].ConnectionStartTime).TotalMinutes)
        .ToArray();

    // 90% 이상이 같은 간격이면 의심
    if (intervals.Length > 5)
    {
        var avgInterval = intervals.Average();
        var similarIntervals = intervals.Count(i => Math.Abs(i - avgInterval) < 0.5);
        return (double)similarIntervals / intervals.Length > 0.9;
    }

    return false;
}
```

### 모니터링 동작

1. **상세 로깅**: 모든 네트워크 활동을 SQLite DB에 기록
2. **패턴 분석**: 머신러닝 기반 행동 패턴 분석 준비
3. **사용자 알림**: 비침습적 상태바 알림
4. **주기적 리포트**: 일일/주간 보안 리포트에 포함
5. **화이트리스트 학습**: 사용자 승인 시 화이트리스트 자동 업데이트

---

## 🛠️ 구현 우선순위

### Phase 1: 기본 인프라 (1-2주)

- [ ] 기본 차단 시스템 프레임워크
- [ ] SQLite 로깅 시스템
- [ ] 설정 관리 시스템
- [ ] 사용자 알림 UI

### Phase 2: 핵심 탐지 로직 (2-3주)

- [ ] 1단계 즉시 차단 규칙 구현
- [ ] 2단계 경고 시스템 구현
- [ ] 방화벽 연동 기능
- [ ] 프로세스 종료 기능

### Phase 3: 고급 기능 (3-4주)

- [ ] 3단계 모니터링 시스템
- [ ] GeoIP 기반 국가별 차단
- [ ] 디지털 서명 검증
- [ ] 패턴 학습 시스템

### Phase 4: 최적화 및 확장 (4-6주)

- [ ] 성능 최적화
- [ ] 머신러닝 기반 패턴 분석
- [ ] 클라우드 위협 인텔리전스 연동
- [ ] 관리자 대시보드

---

## ⚙️ 설정 관리

### 차단 정책 설정

```json
{
  "AutoBlockSettings": {
    "EnableLevel1": true,
    "EnableLevel2": true,
    "EnableLevel3": true,
    "WarningTimeout": 10,
    "WhitelistedProcesses": ["chrome.exe", "firefox.exe", "steam.exe"],
    "WhitelistedIPs": ["8.8.8.8", "1.1.1.1"],
    "BlockedCountries": ["CN", "RU", "KP"],
    "CustomRules": [
      {
        "Name": "Custom Rule 1",
        "Pattern": "*.suspicious-domain.com",
        "Action": "Block",
        "Level": 1
      }
    ]
  }
}
```

### 로그 보존 정책

```json
{
  "LogSettings": {
    "MaxLogSizeMB": 100,
    "RetentionDays": 30,
    "DetailLevel": "Medium",
    "ExportFormat": "JSON"
  }
}
```

---

## 🔒 보안 고려사항

### 우회 방지

- 프로세스 이름 변경 대응
- DLL 인젝션 탐지
- 메모리 패치 방지
- 시스템 서비스 권한 악용 방지

### 오탐 최소화

- 화이트리스트 기반 예외 처리
- 사용자 피드백 반영 시스템
- 점진적 학습 알고리즘
- A/B 테스트를 통한 규칙 검증

### 성능 영향 최소화

- 비동기 처리로 UI 블로킹 방지
- 배치 처리로 시스템 부하 분산
- 캐싱을 통한 중복 분석 방지
- 리소스 모니터링 및 자동 조절

이 규칙 체계를 바탕으로 단계적으로 구현하면 효과적이고 안전한 자동 차단 시스템을 만들 수 있습니다.
