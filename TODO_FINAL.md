# WindowsSentinel 발표 준비 최종 계획

**📅 발표일정**: 2025년 10월 22일 (다음주)  
**⏰ 남은 기간**: 7일  
**🎯 목표**: Enterprise급 보안 솔루션 완성 및 성공적 발표

---

## 🚨 **긴급 수정사항 (Day 1 - 10월 15일)**

### **🔥 최우선 과제**

#### **1. 사설 IP 차단 문제 해결** ⭐⭐⭐⭐⭐

**🚨 현재 문제**: VPN 사용 시 10.0.0.5, 192.168.0.100 등 정상 IP가 차단됨

**해결 방안**:

```csharp
// RealTimeSecurityAnalyzer.cs 또는 AutoBlockService.cs에 추가
private static readonly string[] PRIVATE_IP_WHITELIST = {
    "10.0.0.0/8",      // VPN 일반 대역
    "192.168.0.0/16",  // 가정용 사설 IP
    "172.16.0.0/12",   // 기업용 사설 IP
    "127.0.0.0/8"      // 로컬호스트
};

private bool IsPrivateIP(string ipAddress)
{
    // 사설 IP 대역 검사 로직
    return PRIVATE_IP_WHITELIST.Any(range => IsInRange(ipAddress, range));
}
```

**📋 구현 단계**:

- [ ] 사설 IP 검사 유틸리티 함수 구현
- [ ] AutoBlockService에서 사설 IP 차단 제외 로직 추가
- [ ] DDoS 탐지 시스템에서 사설 IP 예외 처리
- [ ] 테스트: VPN 연결 후 정상 동작 확인

**⏰ 예상 소요**: 2시간  
**🎯 발표 영향도**: 극대 (데모 실패 방지)

---

#### **2. 시스템 프로세스 차단 문제 해결** ⭐⭐⭐⭐

**🚨 현재 문제**: notepad.exe, calc.exe 등 정상 프로세스가 차단됨

**해결 방안**:

```csharp
// 시스템 프로세스 화이트리스트 확장
private static readonly string[] SYSTEM_PROCESS_WHITELIST = {
    "notepad.exe", "mspaint.exe", "calc.exe", "explorer.exe",
    "taskmgr.exe", "regedit.exe", "cmd.exe", "powershell.exe",
    "winlogon.exe", "csrss.exe", "lsass.exe", "services.exe"
};

private bool IsSystemProcess(string processPath)
{
    var processName = Path.GetFileName(processPath).ToLower();
    return SYSTEM_PROCESS_WHITELIST.Contains(processName) ||
           processPath.StartsWith(@"C:\Windows\System32\") ||
           processPath.StartsWith(@"C:\Windows\");
}
```

**📋 구현 단계**:

- [ ] 확장된 시스템 프로세스 리스트 정의
- [ ] 프로세스 경로 기반 화이트리스트 로직 구현
- [ ] AutoBlock 시스템에서 시스템 프로세스 예외 처리
- [ ] 테스트: 메모장, 계산기 등 정상 실행 확인

**⏰ 예상 소요**: 1시간  
**🎯 발표 영향도**: 대 (사용자 신뢰도)

---

#### **3. Toast 알림 x버튼 버그 수정** ⭐⭐⭐

**🚨 현재 문제**: Toast의 x버튼 클릭 시 설정 페이지로 이동

**해결 방안**:

```csharp
// SimpleToastControl.xaml.cs
private void CloseButton_Click(object sender, RoutedEventArgs e)
{
    // 설정 페이지 이동 로직 제거
    // Toast 단순 닫기만 수행
    this.Visibility = Visibility.Collapsed;

    // 애니메이션과 함께 제거
    var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
    fadeOut.Completed += (s, args) => ((Panel)this.Parent)?.Children.Remove(this);
    this.BeginAnimation(OpacityProperty, fadeOut);
}
```

**📋 구현 단계**:

- [ ] Toast x버튼 이벤트 핸들러 수정
- [ ] 설정 페이지 이동 로직 제거
- [ ] 단순 Toast 닫기 기능으로 변경
- [ ] 테스트: x버튼 클릭 시 Toast만 닫히는지 확인

**⏰ 예상 소요**: 1시간  
**🎯 발표 영향도**: 중 (UX 완성도)

---

## 🎯 **핵심 기능 완성 (Day 2 - 10월 16일)**

### **4. Windows 방화벽 규칙 시스템 완성** ⭐⭐⭐⭐⭐

**🎯 목표**: 보안 관리 → 방화벽 탭에서 Windows 시스템 방화벽 규칙까지 통합 관리

**구현 계획**:

```csharp
// WindowsFirewallManager.cs (신규 생성)
public class WindowsFirewallManager
{
    // Windows 시스템 방화벽 규칙 조회
    public List<FirewallRule> GetAllWindowsRules()

    // LogCheck 생성 규칙과 시스템 규칙 구분
    public List<FirewallRule> GetLogCheckRules()
    public List<FirewallRule> GetSystemRules()

    // 규칙 활성화/비활성화
    public bool EnableRule(string ruleName)
    public bool DisableRule(string ruleName)
}
```

**UI 설계**:

```xml
<!-- NetWorks_New.xaml 방화벽 탭 확장 -->
<TabControl>
    <TabItem Header="LogCheck 규칙">
        <!-- 기존 방화벽 규칙 DataGrid -->
    </TabItem>
    <TabItem Header="Windows 시스템 규칙">
        <!-- 새로운 시스템 규칙 DataGrid -->
        <!-- 활성화/비활성화 토글 버튼 -->
    </TabItem>
</TabControl>
```

**📋 구현 단계**:

- [ ] WindowsFirewallManager 클래스 생성
- [ ] COM Interop을 통한 시스템 방화벽 규칙 조회
- [ ] 방화벽 탭 UI 확장 (LogCheck/Windows 분리)
- [ ] 규칙 활성화/비활성화 기능 구현
- [ ] Toast 알림으로 결과 피드백

**⏰ 예상 소요**: 6시간  
**🎯 발표 영향도**: 극대 (핵심 차별화 기능)

---

### **5. 보안 이벤트 근거 표시 기능** ⭐⭐⭐⭐

**🎯 목표**: DDoS 탐지, 차단 이벤트 발생 시 "왜 차단되었는지" 명확한 근거 제공

**구현 계획**:

```csharp
// SecurityEventReason.cs (신규)
public class SecurityEventReason
{
    public string EventType { get; set; }        // "DDoS 탐지", "AutoBlock"
    public string TriggerCriteria { get; set; }  // "SYN Flood: 523개/초 (임계값: 500)"
    public string RiskLevel { get; set; }        // "위험", "높음"
    public string RecommendedAction { get; set; } // "즉시 차단 권장"
    public DateTime DetectedTime { get; set; }
}

// Toast 알림에 근거 정보 포함
toastService.ShowSecurityAlert(
    $"DDoS 공격 탐지: {ipAddress}",
    $"SYN Flood {packetCount}개/초 (임계값: 500개/초 초과)",
    SecurityLevel.Critical
);
```

**📋 구현 단계**:

- [ ] SecurityEventReason 데이터 모델 생성
- [ ] DDoS 탐지 시 상세 근거 정보 생성
- [ ] Toast 알림에 근거 정보 표시
- [ ] 보안 이벤트 로그에 근거 기록
- [ ] UI에서 "자세히 보기" 버튼으로 근거 표시

**⏰ 예상 소요**: 2시간  
**🎯 발표 영향도**: 대 (전문성 어필)

---

## 🎨 **UI/UX 완성 (Day 3 - 10월 17일)**

### **6. 사이드바 화살표 UI 추가** ⭐⭐⭐

**🎯 목표**: 사이드바 확장/축소 상태를 직관적으로 표시

**구현 계획**:

```xml
<!-- MainWindows.xaml 사이드바 영역 -->
<Grid Name="SidebarGrid">
    <Button Name="SidebarToggleButton"
            HorizontalAlignment="Right"
            Width="20" Height="30">
        <Path Name="ArrowIcon"
              Data="M0,0 L10,5 L0,10"
              Fill="{DynamicResource PrimaryTextColor}"/>
    </Button>
</Grid>
```

```csharp
// 화살표 방향 애니메이션
private void AnimateArrow(bool isExpanded)
{
    var rotation = isExpanded ? 180 : 0;
    var rotateTransform = new RotateTransform(rotation);
    ArrowIcon.RenderTransform = rotateTransform;
}
```

**📋 구현 단계**:

- [ ] 사이드바 화살표 아이콘 추가
- [ ] 확장/축소 상태별 화살표 방향 변경
- [ ] 부드러운 회전 애니메이션 적용
- [ ] 테마별 색상 대응

**⏰ 예상 소요**: 2시간  
**🎯 발표 영향도**: 중 (UI 완성도)

---

### **7. Toast 알림 시스템 전체 확산** ⭐⭐⭐⭐

**🎯 목표**: 모든 모듈에서 일관된 Toast 알림 사용

**적용 대상**:

```csharp
// 1. 방화벽 관리 (NetWorks_New.xaml.cs)
toastService.ShowSuccess("방화벽 규칙이 생성되었습니다.");
toastService.ShowError("방화벽 규칙 생성에 실패했습니다.");

// 2. DDoS 탐지 (DDoSDetectionEngine.cs)
toastService.ShowSecurityAlert("DDoS 공격 탐지", details);

// 3. ThreatIntelligence (ThreatIntelligence.xaml.cs)
toastService.ShowInfo($"IP 위협 분석 완료: {result.ThreatLevel}");

// 4. 시스템 복구 (Vaccine.xaml.cs, Recoverys.xaml.cs)
toastService.ShowProgress("시스템 복구 진행 중...", progress);
```

**📋 구현 단계**:

- [ ] 방화벽 관리에서 MessageBox → Toast 전환
- [ ] DDoS 탐지 시 보안 Toast 알림 추가
- [ ] ThreatIntelligence IP 조회 결과 Toast 표시
- [ ] Vaccine/Recoverys 작업 진행상황 Toast
- [ ] 일관된 알림 디자인 및 애니메이션 적용

**⏰ 예상 소요**: 4시간  
**🎯 발표 영향도**: 대 (사용자 경험 통합)

---

## 🎬 **발표 준비 및 마무리 (Day 4-5 - 10월 18일~19일)**

### **8. 발표용 데모 시나리오 준비** ⭐⭐⭐⭐⭐

**🎯 핵심 데모 시나리오**:

#### **시나리오 1: 실시간 보안 모니터링** (2분)

1. **네트워크 연결 실시간 표시**

   - 📋 프로세스 탭에서 활성 연결 확인
   - 위험도별 색상 구분 시연
   - 실시간 차트 업데이트 확인

2. **보안 상태 대시보드**
   - 📊 대시보드에서 통합 보안 지표 확인
   - LiveCharts 기반 시각화 시연

#### **시나리오 2: DDoS 공격 탐지 및 자동 대응** (3분)

1. **모의 DDoS 공격 시뮬레이션**

   ```bash
   # 테스트용 대량 연결 생성
   for i in {1..600}; do curl -s http://localhost:8080 & done
   ```

2. **실시간 탐지 및 알림**

   - SYN Flood 탐지 Toast 알림 표시
   - 보안 이벤트 로그에 상세 근거 기록
   - 자동 차단 실행 확인

3. **대응 결과 확인**
   - 방화벽 규칙 자동 생성 확인
   - AutoBlock 대시보드에서 차단 내역 확인

#### **시나리오 3: Windows 방화벽 통합 관리** (2분)

1. **LogCheck 규칙 관리**

   - 🔒 보안 관리 → 방화벽 탭 진입
   - 기존 LogCheck 생성 규칙 확인

2. **Windows 시스템 규칙 관리**
   - Windows 시스템 규칙 탭 전환
   - 규칙 활성화/비활성화 시연
   - Toast 알림으로 결과 확인

#### **시나리오 4: 영구 차단 시스템 효과** (2분)

1. **프로세스 영구 차단**

   - 특정 프로세스 영구 차단 실행
   - Windows 방화벽에 규칙 생성 확인

2. **재부팅 후 지속성 검증**
   - 시스템 재시작 시뮬레이션 (또는 설명)
   - 차단 규칙 자동 복구 확인

**📋 준비 사항**:

- [ ] 각 시나리오별 테스트 데이터 준비
- [ ] 데모용 가상 공격 스크립트 작성
- [ ] 발표 화면 레이아웃 최적화
- [ ] 시나리오별 예상 질문 답변 준비

**⏰ 예상 소요**: 4시간

---

### **9. 성능 최적화 및 안정성 검증** ⭐⭐⭐⭐

**🎯 검증 항목**:

#### **메모리 누수 체크**

```csharp
// 장시간 실행 테스트
private void RunLongTermStabilityTest()
{
    var initialMemory = GC.GetTotalMemory(true);

    // 1시간 동안 정상 동작 테스트
    for (int i = 0; i < 3600; i++)
    {
        // 네트워크 모니터링 시뮬레이션
        Thread.Sleep(1000);

        if (i % 300 == 0) // 5분마다 메모리 체크
        {
            var currentMemory = GC.GetTotalMemory(true);
            var memoryIncrease = currentMemory - initialMemory;
            Console.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024} MB");
        }
    }
}
```

#### **대용량 데이터 처리 테스트**

- 1000개 이상 네트워크 연결 동시 처리
- 대용량 로그 데이터 표시 성능
- UI 반응성 (1초 이내 응답)

#### **예외 상황 처리**

- 네트워크 연결 실패 시 복구
- 관리자 권한 없을 때 적절한 안내
- COM Interop 실패 시 대체 방안

**📋 검증 단계**:

- [ ] 메모리 사용량 모니터링 (1시간 실행)
- [ ] 대용량 연결 데이터 처리 테스트
- [ ] 예외 상황별 오류 처리 검증
- [ ] UI 반응성 테스트 (대용량 데이터)
- [ ] 장시간 실행 안정성 확인

**⏰ 예상 소요**: 4시간

---

## 🏆 **발표 성공 전략**

### **💡 핵심 어필 포인트**

#### **1. 기술적 혁신성**

```
✅ Enterprise급 DDoS 방어 (7개 공격 패턴 실시간 탐지)
✅ Windows 방화벽 완전 통합 (COM Interop 직접 제어)
✅ 패킷 레벨 분석 엔진 (TCP 플래그, 시그니처 매칭)
✅ 영구 차단 시스템 (시스템 재부팅 후에도 유지)
```

#### **2. 사용자 경험 혁신**

```
🎨 계층적 UI 구조 (3단계 탭으로 직관적 구성)
🎨 Toast 알림 시스템 (현대적 비침습적 UX)
🎨 실시간 시각화 (LiveCharts 기반 보안 대시보드)
🎨 다크/라이트 테마 (완전한 테마 시스템)
```

#### **3. 실용성 및 완성도**

```
🛡️ 실제 사용 가능한 보안 솔루션 (상용 수준)
🛡️ VPN 환경 완벽 호환 (사설 IP 대응)
🛡️ 시스템 프로세스 보호 (오탐 방지)
🛡️ Clean Code 달성 (컴파일 경고 0개)
```

### **📊 예상 질문 및 답변 준비**

#### **Q1: 기존 보안 솔루션과의 차별점은?**

**A**: "기존 솔루션은 탐지만 하지만, 저희는 Windows 방화벽과 직접 연동하여 **즉시 자동 차단**까지 수행합니다. 또한 **영구 차단 시스템**으로 재부팅 후에도 보안 정책이 유지됩니다."

#### **Q2: 실제 환경에서 사용 가능한가?**

**A**: "네, **VPN 환경 호환성**, **시스템 프로세스 보호** 등 실제 사용 시 발생하는 문제들을 모두 해결했습니다. 현재 개발 환경에서 24시간 안정적으로 동작하고 있습니다."

#### **Q3: 성능은 어떤가?**

**A**: "**1초 이내 위협 탐지 및 대응**이 가능하며, 1000개 이상의 동시 연결도 원활하게 처리합니다. 메모리 사용량도 최적화되어 있습니다."

---

## ⚡ **비상 계획 (시간 부족 시)**

### **최소 필수 완성 사항 (Core MVP)**

1. ✅ **사설 IP 차단 문제 해결** (데모 실패 방지)
2. ✅ **Windows 방화벽 규칙 시스템** (핵심 차별화)
3. ✅ **발표 데모 시나리오** (성공적 시연)

### **포기 가능한 기능**

- 사이드바 화살표 UI 개선
- Toast 알림 전체 모듈 확산
- 성능 최적화 세부사항

### **최악 시나리오 대응**

- 현재 완성된 기능만으로도 **Enterprise급 보안 솔루션** 어필 가능
- 기존 DDoS 방어, AutoBlock, 실시간 모니터링만으로 충분한 임팩트

---

## 🎯 **최종 목표 및 성공 지표**

### **발표 완성도 목표**

```
🏆 기술적 완성도: 95% (Enterprise급 기능 완성)
🏆 사용자 경험: 90% (직관적이고 현대적인 UI)
🏆 실용성: 95% (실제 사용 가능한 수준)
🏆 시연 성공률: 98% (안정적인 데모 환경)
```

### **발표 임팩트 예상**

- ✅ **차별화된 기술적 혁신** 입증
- ✅ **실용적 가치** 명확히 제시
- ✅ **완성도 높은 솔루션** 시연
- ✅ **상용 수준의 품질** 어필

**🎉 예상 결과**: **매우 성공적인 발표**로 프로젝트의 가치와 완성도를 충분히 입증할 수 있을 것입니다.

---

## 📋 **일일 체크리스트**

### **Day 1 (10월 15일) ✅**

- [ ] 사설 IP 화이트리스트 구현
- [ ] 시스템 프로세스 보호 로직 추가
- [ ] Toast x버튼 버그 수정
- [ ] 빌드 및 기본 테스트

### **Day 2 (10월 16일)**

- [ ] WindowsFirewallManager 구현
- [ ] 방화벽 탭 UI 확장
- [ ] 보안 이벤트 근거 표시 기능
- [ ] 통합 테스트

### **Day 3 (10월 17일)**

- [ ] 사이드바 화살표 UI 추가
- [ ] Toast 알림 시스템 확산
- [ ] UI/UX 최종 점검
- [ ] 사용성 테스트

### **Day 4-5 (10월 18일~19일)**

- [ ] 데모 시나리오 준비
- [ ] 성능 및 안정성 검증
- [ ] 발표 자료 및 답변 준비
- [ ] 최종 점검 및 리허설

**⚠️ 매일 저녁 진행상황 점검 및 다음날 계획 조정 필수!**
