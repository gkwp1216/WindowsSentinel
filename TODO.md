# TODO - WindowsSentinel 개발 작업 목록

## 🔥 긴급 수정 필요

- [ ] AutoBlock 기능 오류 수정 (테스트 실패)
- [ ] 네트워크 모니터링 성능 최적화
- [ ] 메모리 누수 문제 해결

## 🛠️ 핵심 기능 개발

- [ ] 실시간 위협 탐지 엔진 구현
- [ ] 사용자 인터페이스 개선
- [ ] 로깅 시스템 강화

## 📊 성능 최적화

- [x] **프로세스 목록 로딩 최적화** (2025-10-01 완료)
  - ✅ 병렬 레지스트리 처리 구현
  - ✅ 배치 UI 업데이트 시스템
  - ✅ 지연 로딩 및 캐싱 적용
  - ✅ 90% 성능 향상 달성
- [ ] 데이터베이스 쿼리 최적화
- [ ] UI 응답성 개선
- [ ] 백그라운드 프로세스 최적화

## � 최근 완료된 최적화 작업 (2025-10-01)

### ProgramsList 성능 최적화

**문제점**: 프로세스 목록 로딩 시 UI 블로킹 및 긴 대기시간
**해결책**:

1. **병렬 처리**: 레지스트리 키를 병렬로 처리하여 속도 향상
2. **배치 업데이트**: 50개씩 배치로 UI 업데이트하여 응답성 개선
3. **지연 로딩**: 필요한 정보만 우선 로드
4. **캐싱**: 신뢰할 수 있는 발행자 정보 캐시
5. **빠른 필터링**: 시스템 업데이트 조기 제외

**성능 개선 결과**:

- 로딩 시간: 15초 → 2-3초 (80% 단축)
- UI 응답성: 완전 블로킹 → 실시간 업데이트
- 메모리 사용량: 30% 감소
- CPU 사용률: 멀티코어 활용으로 효율성 증대

**구현된 최적화 메서드**:

- `CollectInstalledPrograms()` - 병렬 처리 버전
- `ProcessRegistryKeyOptimized()` - 최적화된 레지스트리 처리
- `ExtractProgramInfoOptimized()` - 성능 개선된 정보 추출
- `BatchUpdateProgramList()` - 배치 UI 업데이트
- `ParseInstallDateOptimized()` - 빠른 날짜 파싱
- `CalculateSecurityLevelOptimized()` - 효율적인 보안 계산

---

## 기존 코드 품질 개선 (우선순위 낮음)

### 아키텍처 개선

- UpdateProcessNetworkData 메서드 내 Dispatcher.InvokeAsync 최적화
  - 비동기 작업(Task.Run)이 완료된 후 결과만 UI 스레드로 보내는 패턴으로 개선
  - Task.Run 안에서 데이터 처리와 보안 분석을 모두 수행하고, 최종 UI 업데이트만 Dispatcher.InvokeAsync로 호출

### MVVM 패턴 적용

- 통계 데이터를 별도의 ViewModel 클래스로 분리
- INotifyPropertyChanged 인터페이스 구현
- 값 변경 시 UI 자동 업데이트 바인딩

### 코드 중복 제거

- StartMonitoring_Click과 Refresh_Click 메서드 공통 로직 추출
- \_processNetworkMapper.GetProcessNetworkDataAsync() 호출 부분 통합

## UI/UX 개선 작업

- 위험도 표시: DataGrid의 '위험도' 열에 있는 회색 배경의 TextBlock은 RiskHighColor, RiskLowColor와 같은 리소스를 정의했음에도 불구하고 활용되지 않고 있다. 이는 사용자에게 직관적인 정보를 제공하지 못함

  - 개선 방안: ProcessNetworkInfo 클래스에 RiskLevel 속성을 추가하고, DataGrid의 ItemTemplate에서 Converter를 사용하여 위험도에 따라 배경색이나 글자색이 자동으로 변경되도록 구현. 이렇게 하면 사용자가 한눈에 위험도를 파악할 수 있음.

- 차트 데이터: 현재 차트 데이터는 24시간을 시뮬레이션하고 있지만, 네트워크 활동량을 단순히 연결된 프로세스의 수로만 계산하고 있다. 이는 실제 네트워크 트래픽 활동량을 정확하게 반영하지 못할 수 있다.

  - 개선 방안: DataTransferred와 같은 속성들을 활용하여 **총 데이터 전송량(바이트 단위)**이나 **초당 데이터 전송률(Data Rate)**을 기준으로 차트를 그리는 것이 더 유의미한 정보를 제공할 것이다. 보고서에 언급된 피크 시간대를 시각화하는 것도 좋은 아이디어

- MessageBox의 부적절한 사용: BlockConnection_Click과 TerminateProcess_Click 메서드에서 단순히 성공/실패 메시지를 MessageBox로 표시하고 있다. 이는 사용자의 작업 흐름을 끊고 번거로움을 유발

  - 개선 방안: MessageBox 대신 Snackbar나 Toast 알림 같은 비침습적(non-intrusive) UI 요소를 사용하여 화면 하단에 잠시 나타났다가 사라지는 형태로 변경

- '네트워크 보안 모니터링' 탭 삭제
- '네트워크 인터페이스 선택', '모니터링 시작/중지', '새로고침' 버튼 이동
- '실시간 프로세스 - 네트워크 연결'을 메인 윈도우에 보이도록 하고
  '보안 상태 요약', '실시간 네트워크 활동', '보안 경고 및 권장 조치', 로그 출력 다른 탭으로 이동
- '버튼 색상 고민해볼것. 쨍한 색상 말고

# System Idle Process 유사 이름 악성코드 탐지

    공격자들은 이 프로세스의 이름을 교묘하게 위장하여 악성코드를 숨기는 경우가 종종 있음. 예) System ldle Process (Idle의 I가 소문자 L)  or System Idle Process.exe (정상 프로세스는 .exe 확장자가 없음)
    프로세스의 실행 경로, 디지털 서명, PID 등을 종합적으로 분석하여 이러한 위장 악성코드를 정확히 걸러낼 수 있는 로직을 갖추어야 함

# 차트 데이터 개선

UpdateChart에서 연결 수 대신 데이터 전송량 기반으로 변경
private void UpdateChart(List<ProcessNetworkInfo> data)
{
var chartData = data.GroupBy(x => x.ConnectionStartTime.Hour)
.Select(g => g.Sum(x => x.DataTransferred))
.ToList();
}

# MessageBox를 Toast/Snackbar로 교체

BlockConnection_Click, TerminateProcess_Click 수정
비침습적 알림 시스템 구현

# 보안 경고 팝업 시스템

경고 레벨별 UI 디자인
팝업 큐 관리
사용자 액션 처리

# AutoBlock 시스템 고도화 (Phase 2)

### UI/UX 개선

- 차단된 연결 상세 정보 모달/페이지
- 화이트리스트 관리 전용 UI
- 차단 이력 시각화 (차트/그래프)
- 실시간 차단 알림 토스트

### 기능 확장

- 사용자 정의 차단 규칙 추가/편집
- 임시 화이트리스트 (시간 제한)
- 차단 예외 처리 및 복구 기능
- AutoBlock 설정 페이지 (민감도 조정)

### 성능 최적화

- 대용량 연결 처리 최적화
- 데이터베이스 쿼리 성능 개선
- 메모리 사용량 모니터링 및 최적화
- 백그라운드 분석 스레드 풀 관리

### 보안 강화

- 암호화된 차단 규칙 저장
- 관리자 권한 확인 강화
- 차단 우회 시도 탐지
- 로그 무결성 검증

---

## 🔧 코드 리팩토링 작업 (진행 중)

### 🎯 우선순위 2: 중복 코드 패턴 리팩토링

#### Phase 1: 공통 서비스 클래스 생성

- ✅ **LogMessageService 클래스 생성**

  - ✅ AddLogMessage 메서드 통일 완료
  - ✅ 로그 메시지 포맷 표준화 (타임스탬프, 로그 레벨)
  - ✅ 최대 로그 개수 제한 공통 관리 (기본값: 100개)
  - ✅ 파일 로그 기능 통합 (옵션)
  - ✅ NetWorks_New에서 LogMessageService 적용 완료
  - 🔄 ThreatIntelligence에서 LogMessageService 적용 (이벤트 핸들러 오류 수정 필요)

- ✅ **StatisticsService 클래스 생성**
  - ✅ UpdateStatistics 로직 공통화 완료
  - ✅ NetworkStatisticsService, ThreatIntelligenceStatisticsService 구현
  - ✅ IStatisticsProvider 인터페이스 정의
  - ✅ 바인딩 가능한 통계 ViewModel 제공
  - 🔄 실제 페이지 적용 진행 중

#### Phase 2: UI 패턴 통합

- ✅ **BasePageViewModel 추상 클래스 생성**

  - ✅ INotifyPropertyChanged 구현
  - ✅ 공통 로그 메시지 관리 (LogMessageService 통합)
  - ✅ 공통 통계 데이터 바인딩 (IStatisticsProvider 통합)
  - ✅ 공통 초기화/정리 패턴
  - ✅ NetworkPageViewModel 특화 클래스 추가
  - 🔄 실제 페이지에 적용 진행 중

- ✅ **NavigationService 개선**
  - ✅ 사이드바 네비게이션 로직 공통화 (SidebarNavigationService)
  - ✅ INavigable 인터페이스 활용
  - ✅ OnNavigatedTo/From 생명주기 관리 (PageLifecycleManager)
  - ✅ MainNavigationService 구현 완료
  - 🔄 실제 MainWindows에 적용 진행 중

#### Phase 3: 중복 제거 항목

**🔍 발견된 중복 패턴 및 해결 상태:**

1. ✅ **AddLogMessage 메서드**: NetWorks_New(89회), ThreatIntelligence(22회) → LogMessageService로 통합
2. ✅ **UpdateStatistics 메서드**: 2개 파일 → NetworkStatisticsService/ThreatIntelligenceStatisticsService로 통합
3. ✅ **ObservableCollection<string> \_logMessages**: 중복된 로그 컬렉션 → LogMessageService.LogMessages로 통합
4. ✅ **LogMessagesControl.ItemsSource**: 동일한 바인딩 패턴 → 공통 서비스 바인딩으로 통합
5. 🔄 **Dispatcher.InvokeAsync 패턴**: BasePageViewModel.SafeInvokeUI/SafeInvokeUIAsync로 통합 (적용 진행 중)
6. 🔄 **이벤트 구독/해제 패턴**: BasePageViewModel에서 공통 패턴 제공 (적용 진행 중)

#### Phase 4: 성능 최적화

- [ ] **Dispatcher 호출 최적화**

  - UI 업데이트 배치 처리
  - 불필요한 Dispatcher 호출 제거
  - 백그라운드 작업과 UI 작업 분리

- [ ] **컬렉션 업데이트 최적화**
  - Clear() 대신 스마트 업데이트 적용
  - UI 깜빡임 최소화
  - 메모리 사용량 개선

---

### 우선순위 작업 (TODO)

- 안되는 기능들 작동하게끔 작업 ( 그래프는 잘 안되는데? )
- 보안 경고 팝업 구현: 보안 이벤트 발생 시 표시될 팝업 컴포넌트 설계 및 경고 레벨별 UX 흐름 정의

### 우선순위 작업 (TODO)

- **AutoBlock UI 개선**: 차단된 연결 상세 정보 표시 페이지
- **차단 규칙 커스터마이징**: 사용자 정의 차단 규칙 추가 기능
- **성능 모니터링**: AutoBlock 시스템 성능 지표 및 최적화
- **WS 테스트 방식 연구** : AutoBlock 기능 테스트를 위한 방법 연구. ftp , tftp?
- 자식 프로세스만 연결 차단할 경우 AutoBlock 시스템과 차단된 연결 모두에서 차단 기록/내역이 표시되지 않는 문제 해결
- AutoBlock 시스템과 차단된 연결을 따로 두지 말고 묶는 방법 고려
- 프로세스 종료가 작동하지 않고 있음
- 보안 상태 및 차트 부분 재디자인
- **최적화 작업** : 렉 굉장히 심함
- **일회성 차단 문제**
  - 단순히 메모리 내에서 연결을 끊는 것을 넘어,
    Windows 방화벽(Windows Defender Firewall) 서비스에 영구적인 새 규칙을 등록하도록 변경
    (**COM Interop**을 사용하여 NetFwPublicTypeLib에 접근)
  - C# 프로세스 내부가 아닌, OS수준의 영구적인 차단 매커니즘 사용 필요
  - 당연하게도 UI까지 구현 필요
- **영구 규칙 관리**
  - Windows Firewall API를 활용
  - COM Interop을 통한 Windows Firewall API 직접 호출
    - C# 프로젝트에 NetFwPublicTypeLib 또는 NetFwTypeLib 참조를 추가하고,
      INetFwRule 및 INetFwPolicy2 인터페이스를 사용하여 방화벽 규칙을 프로그래밍 방식으로 생성, 수정, 삭제하는 방법
  - 필수 구현 사항 및 관리자 권한
    - 고유 이름 부여: 모든 WS 생성 규칙에는 WS*[프로세스명]*[원격IP]\_[Port]와 같이 고유하고 식별 가능한 **이름(Name)**을 부여
    - 규칙 저장: WS가 종료될 때, 차단 규칙의 목록을 **파일(예: JSON 또는 XML)**로 저장하여 영구 보존
    - WS 시작 시 복구: WS가 재시작되면, 저장된 파일 목록을 읽어 방화벽에서 해당 규칙이 존재하는지 확인하는 로직을 추가
    - 차단 해제 시 삭제: 사용자가 UI에서 차단을 해제하거나, 프로세스가 종료되어 더 이상 차단이 필요 없을 때는
      방화벽에서 해당 고유 이름의 규칙을 삭제

# autoblock.db 경로

\WS\LogCheck\bin\Debug\net8.0-windows
