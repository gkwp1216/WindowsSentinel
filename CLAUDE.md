# LogCheck 프로젝트 Claude 에이전트 가이드# LogCheck 프로젝트 Claude 에이전트 가이드

## 🎯 **프로젝트 개요**## 🎯 **프로젝트 개요**

- **프로젝트명**: LogCheck (네트워크 보안 모니터링)- **프로젝트명**: LogCheck (네트워크 보안 모니터링)

- **저장소**: WindowsSentinel- **저장소**: WindowsSentinel

- **기술스택**: .NET 8 WPF + SharpPcap + MaterialDesign- **기술스택**: .NET 8 WPF + SharpPcap + MaterialDesign

- **언어**: C# (UI/문서는 한국어)- **언어**: C# (UI/문서는 한국어)

- **현재상태**: 네임스페이스 통일 완료 ✅- **현재상태**: 네임스페이스 통일 완료 ✅

- **최종업데이트**: 2025-09-17- **최종업데이트**: 2025-09-17 ㅁ

---

## 📋 **핵심 참고 문서**## 📋 **핵심 참고 문서**

- `TODO.md`: 현재 작업 우선순위 및 요구사항- `TODO.md`: 현재 작업 우선순위 및 요구사항

- `DONE.md`: 완료된 기능 및 성과- `DONE.md`: 완료된 기능 및 성과

- `CODING_STANDARDS.md`: 코딩 스타일 가이드라인- `CODING_STANDARDS.md`: 코딩 스타일 가이드라인

- `docs/monitoring-architecture.md`: 기술 아키텍처 설계서- `docs/monitoring-architecture.md`: 기술 아키텍처 설계서

- `LogCheck/LogCheck.sln`: 메인 솔루션 파일- `LogCheck/LogCheck.sln`: 메인 솔루션 파일

---

## 🏗️ **현재 아키텍처 현황**## 🏗️ **현재 아키텍처 현황**

### ✅ **완료된 인프라**### ✅ **완료된 인프라**

- **네임스페이스 통일**: 모든 `WindowsSentinel` → `LogCheck` 변환 완료- **네임스페이스 통일**: 모든 `WindowsSentinel` → `LogCheck` 변환 완료

- **MonitoringHub**: 중앙집중식 모니터링 제어 싱글톤 패턴- **MonitoringHub**: 중앙집중식 모니터링 제어 싱글톤 패턴

- **ICaptureService/CaptureService**: 패킷 캡처 추상화 레이어- **ICaptureService/CaptureService**: 패킷 캡처 추상화 레이어

- **설정 지속성**: AutoSelectNic, BpfFilter, AutoStartMonitoring- **설정 지속성**: AutoSelectNic, BpfFilter, AutoStartMonitoring

- **트레이 통합**: 시스템 트레이 및 풍선 알림- **트레이 통합**: 시스템 트레이 및 풍선 알림

- **Material Design 테마**: Light/Dark 테마 지원- **Material Design 테마**: Light/Dark 테마 지원

- **빌드 상태**: ✅ 성공 (64개 경고, 오류 0개)- **빌드 상태**: ✅ 성공 (64개 경고, 오류 0개)

### 🔄 **진행 중인 작업**### 🔄 **진행 중인 작업**

- 예외 처리 개선- 예외 처리 개선

- UI 응답성 최적화- UI 응답성 최적화

- 데이터 시각화 향상- 데이터 시각화 향상

---

## 🎯 **우선순위 작업 큐 (항상 TODO.md를 먼저 확인)**## 🎯 **우선순위 작업 큐 (항상 TODO.md를 먼저 확인)**

### 🔴 **긴급 (1-2일)**### 🔴 **긴급 (1-2일)**

1. **OnHubMonitoringStateChanged 예외 수정**1. **OnHubMonitoringStateChanged 예외 수정**

   - 파일: `NetWorks_New.xaml.cs` 약 232번 라인 - 파일: `NetWorks_New.xaml.cs` 약 232번 라인

   - 문제: Null 참조 및 UI 스레드 안전성 - 문제: Null 참조 및 UI 스레드 안전성

   - 영향: 애플리케이션 안정성 - 영향: 애플리케이션 안정성

2. **System Idle Process 화이트리스트**2. **System Idle Process 화이트리스트**

   - PID 0 프로세스 예외 처리 - PID 0 프로세스 예외 처리

   - 분석에서 제외하되 UI 가시성 유지 - 분석에서 제외하되 UI 가시성 유지

### 🟠 **높은 우선순위 (3-5일)**### 🟠 **높은 우선순위 (3-5일)**

3. **DataGrid에서 PID 그룹화**3. **DataGrid에서 PID 그룹화**

   - 프로세스 ID별로 네트워크 연결 그룹화 - 프로세스 ID별로 네트워크 연결 그룹화

   - CollectionViewSource 그룹화 구현 - CollectionViewSource 그룹화 구현

4. **시스템/일반 프로세스 차별화**4. **시스템/일반 프로세스 차별화**

   - 시각적 구분 (색상 코딩) - 시각적 구분 (색상 코딩)

   - 시스템 프로세스에 대한 강화된 경고 표시 - 시스템 프로세스에 대한 강화된 경고 표시

5. **위험도 표시 개선**5. **위험도 표시 개선**

   - `RiskLevelToBackgroundConverter` 구현 - `RiskLevelToBackgroundConverter` 구현

   - 회색 배경을 색상별 위험도로 교체 - 회색 배경을 색상별 위험도로 교체

### 🟡 **보통 우선순위 (1주일)**### 🟡 **보통 우선순위 (1주일)**

6. **차트 데이터 개선**6. **차트 데이터 개선**

   - 연결 수에서 데이터 전송량으로 전환 - 연결 수에서 데이터 전송량으로 전환

   - 향상된 시계열 시각화 - 향상된 시계열 시각화

7. **MessageBox → Toast 알림**7. **MessageBox → Toast 알림**

   - 침습적인 MessageBox 대화상자 교체 - 침습적인 MessageBox 대화상자 교체

   - 스낵바/토스트 시스템 구현 - 스낵바/토스트 시스템 구현

---

## 🛠️ **작업 원칙**## 🛠️ **작업 원칙**

### **1. 개발 워크플로우**### **1. 개발 워크플로우**

```

1. TODO.md에서 현재 우선순위 확인1. TODO.md에서 현재 우선순위 확인

2. DONE.md로 완료된 작업 이해2. DONE.md로 완료된 작업 이해

3. CODING_STANDARDS.md 가이드라인 준수3. CODING_STANDARDS.md 가이드라인 준수

4. 설계 결정시 monitoring-architecture.md 참조4. 설계 결정시 monitoring-architecture.md 참조

5. 중요 변경 후 빌드 및 테스트5. 중요 변경 후 빌드 및 테스트

6. 진행상황을 해당 .md 파일에 업데이트6. 진행상황을 해당 .md 파일에 업데이트

```

### **2. 코드 품질 기준**### **2. 코드 품질 기준**

- **네임스페이스**: 항상 `LogCheck.*` 계층구조 사용- **네임스페이스**: 항상 `LogCheck.*` 계층구조 사용

- **비동기 메서드**: 예외 처리가 포함된 적절한 async/await 패턴- **비동기 메서드**: 예외 처리가 포함된 적절한 async/await 패턴

- **Null 안전성**: nullable 참조 타입 활성화, null 경우 처리- **Null 안전성**: nullable 참조 타입 활성화, null 경우 처리

- **스레딩**: UI 작업은 Dispatcher 스레드에서만- **스레딩**: UI 작업은 Dispatcher 스레드에서만

- **로깅**: 디버깅을 위한 구조화된 로깅 사용- **로깅**: 디버깅을 위한 구조화된 로깅 사용

- **성능**: CPU <5%, 메모리 <150MB 목표- **성능**: CPU <5%, 메모리 <150MB 목표

### **3. UI/UX 일관성**### **3. UI/UX 일관성**

- **Material Design**: MaterialDesignInXAML 가이드라인 준수- **Material Design**: MaterialDesignInXAML 가이드라인 준수

- **한국어 현지화**: UI 텍스트는 한국어, 코드/주석은 영어- **한국어 현지화**: UI 텍스트는 한국어, 코드/주석은 영어

- **반응형 디자인**: 다양한 화면 크기 지원- **반응형 디자인**: 다양한 화면 크기 지원

- **접근성**: 키보드 내비게이션 및 스크린 리더 지원- **접근성**: 키보드 내비게이션 및 스크린 리더 지원

---

## 🏛️ **아키텍처 준수사항**## 🏛️ **아키텍처 준수사항**

### **데이터 흐름 (monitoring-architecture.md 기준)**### **데이터 흐름 (monitoring-architecture.md 기준)**

```

PacketCapture (SharpPcap) PacketCapture (SharpPcap)

  → Parser (PacketDotNet)   → Parser (PacketDotNet)

    → ProcessNetworkMapper     → ProcessNetworkMapper

      → RealTimeSecurityAnalyzer       → RealTimeSecurityAnalyzer

        → UI 업데이트 + SQLite 저장소        → UI 업데이트 + SQLite 저장소

```

### **구현할 핵심 인터페이스**### **구현할 핵심 인터페이스**

- `ICaptureService`: 패킷 캡처 추상화- `ICaptureService`: 패킷 캡처 추상화

- `IProcessMapper`: PID/포트 해상도- `IProcessMapper`: PID/포트 해상도

- `IAnalyzer`: 보안 규칙 평가- `IAnalyzer`: 보안 규칙 평가

- `IStorage`: 비동기 데이터 지속성- `IStorage`: 비동기 데이터 지속성

### **스레딩 아키텍처**### **스레딩 아키텍처**

- **캡처 스레드**: 백그라운드 패킷 캡처- **캡처 스레드**: 백그라운드 패킷 캡처

- **파서 스레드**: 패킷 파싱 및 분석- **파서 스레드**: 패킷 파싱 및 분석

- **UI 스레드**: Dispatcher를 통한 뷰 업데이트- **UI 스레드**: Dispatcher를 통한 뷰 업데이트

- **저장소 스레드**: 비동기 데이터베이스 작업- **저장소 스레드**: 비동기 데이터베이스 작업

---

## 🔧 **현재 기술 부채**## 🔧 **현재 기술 부채**

### **경고 감소 목표**### **경고 감소 목표**

- **현재**: 64개 경고- **현재**: 64개 경고

- **목표**: <20개 경고- **목표**: <20개 경고

- **중점 영역**:- **중점 영역**:

  - Null 참조 경고 (CS8618, CS8600, CS8625) - Null 참조 경고 (CS8618, CS8600, CS8625)

  - 플랫폼별 API 경고 (CA1416) - 플랫폼별 API 경고 (CA1416)

  - 사용되지 않는 변수/필드 경고 (CS0169, CS0414) - 사용되지 않는 변수/필드 경고 (CS0169, CS0414)

### **성능 최적화**### **성능 최적화**

- 적절한 백프레셔 처리 구현- 적절한 백프레셔 처리 구현

- 데이터베이스 작업용 연결 풀링 추가- 데이터베이스 작업용 연결 풀링 추가

- 대용량 데이터셋을 위한 UI 가상화 최적화- 대용량 데이터셋을 위한 UI 가상화 최적화

---

## 💾 **세션 메모리 & 컨텍스트**## 💾 **세션 메모리 & 컨텍스트**

### **최근 완료된 작업**### **최근 완료된 작업**

- **날짜**: 2025-09-17- **날짜**: 2025-09-17

- **작업**: 네임스페이스 통일 완료- **작업**: 네임스페이스 통일 완료

- **수정된 파일**: - **수정된 파일**:

  - SecurityAnalyzer.cs, NetworkUsageRecord.cs, SecurityStatusItem.cs - SecurityAnalyzer.cs, NetworkUsageRecord.cs, SecurityStatusItem.cs

  - StringToVisibilityConverter.cs, Recoverys.xaml.cs, NetWorks.xaml.cs - StringToVisibilityConverter.cs, Recoverys.xaml.cs, NetWorks.xaml.cs

  - WmiHelper.cs, App.xaml, App.xaml.cs, LogCheck.csproj - WmiHelper.cs, App.xaml, App.xaml.cs, LogCheck.csproj

### **현재 집중 영역**### **현재 집중 영역**

- 예외 처리 및 애플리케이션 안정성- 예외 처리 및 애플리케이션 안정성

- UI 스레드 안전성 개선- UI 스레드 안전성 개선

- 시스템 프로세스 처리 최적화- 시스템 프로세스 처리 최적화

### **다음 세션 연속점**### **다음 세션 연속점**

1. `OnHubMonitoringStateChanged` 예외 수정부터 시작1. `OnHubMonitoringStateChanged` 예외 수정부터 시작

2. 현재 오류 로그 및 크래시 보고서 검토2. 현재 오류 로그 및 크래시 보고서 검토

3. 적절한 null 검사 패턴 구현3. 적절한 null 검사 패턴 구현

4. 다양한 네트워크 조건에서 테스트4. 다양한 네트워크 조건에서 테스트

---

## 🚦 **의사결정 프레임워크**## 🚦 **의사결정 프레임워크**

### **코드 변경 시**### **코드 변경 시**

1. **아키텍처 확인**: monitoring-architecture.md와 일치하는가?1. **아키텍처 확인**: monitoring-architecture.md와 일치하는가?

2. **표준 검증**: CODING_STANDARDS.md를 준수하는가?2. **표준 검증**: CODING_STANDARDS.md를 준수하는가?

3. **영향 고려**: 기존 기능에 영향을 주는가?3. **영향 고려**: 기존 기능에 영향을 주는가?

4. **철저한 테스트**: 빌드, 실행, UI 응답성 검증4. **철저한 테스트**: 빌드, 실행, UI 응답성 검증

5. **변경 문서화**: 관련 .md 파일 업데이트5. **변경 문서화**: 관련 .md 파일 업데이트

### **작업 우선순위 결정 시**### **작업 우선순위 결정 시**

1. **안정성 우선**: 크래시 및 예외 수정1. **안정성 우선**: 크래시 및 예외 수정

2. **사용자 경험**: 응답성 및 피드백 개선2. **사용자 경험**: 응답성 및 피드백 개선

3. **기능 완성**: 반쯤 구현된 기능 완료3. **기능 완성**: 반쯤 구현된 기능 완료

4. **코드 품질**: 기술 부채 감소4. **코드 품질**: 기술 부채 감소

5. **새 기능**: 새로운 기능은 마지막에5. **새 기능**: 새로운 기능은 마지막에

---

## 🎮 **Claude를 위한 빠른 명령어**## 🎮 **Claude를 위한 빠른 명령어**

### **세션 시작**### **세션 시작**

- "TODO.md 확인하고 최고 우선순위 작업 시작해주세요"- "TODO.md 확인하고 최고 우선순위 작업 시작해주세요"

- "현재 빌드 상태 검토하고 다음 단계 확인해주세요"- "현재 빌드 상태 검토하고 다음 단계 확인해주세요"

- "오류 로그 분석하고 수정 계획 세워주세요"- "오류 로그 분석하고 수정 계획 세워주세요"

### **개발 중**### **개발 중**

- "현재 변경사항 빌드하고 검증해주세요"- "현재 변경사항 빌드하고 검증해주세요"

- "진행상황으로 CLAUDE.md 업데이트해주세요"- "진행상황으로 CLAUDE.md 업데이트해주세요"

- "현재 파일에 CODING_STANDARDS.md 적용해주세요"- "현재 파일에 CODING_STANDARDS.md 적용해주세요"

### **세션 종료**### **세션 종료**

- "완료된 작업으로 TODO.md 업데이트해주세요"- "완료된 작업으로 TODO.md 업데이트해주세요"

- "새로운 결정사항이나 패턴 문서화해주세요"- "새로운 결정사항이나 패턴 문서화해주세요"

- "다음 세션 연속점 준비해주세요"- "다음 세션 연속점 준비해주세요"

---

## 📊 **성공 지표**## 📊 **성공 지표**

### **코드 품질**### **코드 품질**

- [ ] 빌드 경고 20개 미만으로 감소- [ ] 빌드 경고 20개 미만으로 감소

- [ ] 정상 작동 중 처리되지 않은 예외 0개- [ ] 정상 작동 중 처리되지 않은 예외 0개

- [ ] 모든 중요 UI 작업 100ms 미만 응답시간- [ ] 모든 중요 UI 작업 100ms 미만 응답시간

- [ ] 메모리 사용량 150MB 이하로 안정- [ ] 메모리 사용량 150MB 이하로 안정

### **사용자 경험**### **사용자 경험**

- [ ] 패킷 캡처 중 UI 정지 없음- [ ] 패킷 캡처 중 UI 정지 없음

- [ ] 부드러운 실시간 데이터 업데이트- [ ] 부드러운 실시간 데이터 업데이트

- [ ] 직관적인 오류 메시지 및 가이드- [ ] 직관적인 오류 메시지 및 가이드

- [ ] 모든 페이지에서 일관된 시각적 디자인- [ ] 모든 페이지에서 일관된 시각적 디자인

### **아키텍처 준수**### **아키텍처 준수**

- [ ] 모든 컴포넌트가 정의된 인터페이스 준수- [ ] 모든 컴포넌트가 정의된 인터페이스 준수

- [ ] 적절한 관심사 분리 (MVVM)- [ ] 적절한 관심사 분리 (MVVM)

- [ ] 비동기 작업이 UI 스레드 차단하지 않음- [ ] 비동기 작업이 UI 스레드 차단하지 않음

- [ ] 데이터베이스 작업이 적절히 배치 처리됨- [ ] 데이터베이스 작업이 적절히 배치 처리됨

---

## 🔄 **정기 유지보수 작업**## 🔄 **정기 유지보수 작업**

### **일일 (각 세션)**### **일일 (각 세션)**

- 빌드 경고 확인 및 해결- 빌드 경고 확인 및 해결

- 예외 로그 검토- 예외 로그 검토

- 중요 사용자 워크플로우 테스트- 중요 사용자 워크플로우 테스트

- 진행상황 문서 업데이트- 진행상황 문서 업데이트

### **주간**### **주간**

- 아키텍처 결정사항 검토 및 업데이트- 아키텍처 결정사항 검토 및 업데이트

- 코드 개선사항 통합- 코드 개선사항 통합

- 성능 벤치마킹- 성능 벤치마킹

- 사용자 피드백 통합- 사용자 피드백 통합

---

---

## 🔥 **최신 세션 작업 내역 (2025-09-17)**

### **프로세스 네트워크 매핑 문제 해결**

#### ✅ **완료된 작업**

1. **ProcessNetworkMapper 분석 완료**

   - 코드 구조 및 로직 검증
   - GetNetworkConnectionsAsync 메서드 정상 작동 확인
   - 기본 아키텍처는 정상임을 확인

2. **netstat 디버깅 완료**

   - netstat 명령어 실행 및 데이터 수집 검증
   - `netstat_debug.txt` 파일을 통한 실제 출력 확인
   - 한국어 netstat 출력 형식 분석: `프로토콜 | 로컬 주소 | 외부 주소 | 상태 | PID`
   - 인코딩 문제 (UTF-8 vs 시스템 기본 인코딩) 파악

3. **문제 원인 규명**

   - **핵심 문제**: netstat 데이터 수집은 정상이나 **파싱 로직 실패**
   - **구체적 원인**:
     - 영어 netstat 출력 가정 vs 한국어 실제 출력
     - 헤더 인식 실패로 인한 데이터 파싱 불가
     - UI에 "N/A" 표시되는 근본 원인 파악

4. **테스트 환경 구축**
   - `TestProcessMapper.cs` 생성
   - ProcessNetworkMapper 단독 테스트 도구 완성
   - 디버그 파일 분석 기능 포함

#### 🔍 **기술적 발견사항**

- **데이터 수집**: ✅ 정상 (netstat 명령어, 프로세스 정보 수집 작동)
- **파일 생성**: ✅ 정상 (디버그 파일 생성 및 데이터 확인 가능)
- **파싱 로직**: ❌ 한국어 netstat 출력 형식 미대응
- **UI 연동**: ❌ 파싱 실패로 인한 "N/A" 표시

#### 📋 **다음 세션 우선 작업**

1. **한국어 netstat 파싱 로직 구현**

   - 현재 `프로토콜`, `로컬 주소`, `외부 주소`, `상태`, `PID` 헤더 형식 대응
   - 인코딩 처리 개선 (UTF-8 vs 시스템 기본)
   - 공백 기반 파싱에서 고정폭 파싱으로 변경 검토

2. **UI 연동 테스트**
   - 수정된 파싱 로직으로 실제 데이터 표시 확인
   - "N/A" 문제 해결 검증
   - 네트워크 모니터링 탭에서 정상 작동 확인

#### 🎯 **세션 성과 지표**

- **분석 완료율**: ✅ 100% (문제 원인 완전 파악)
- **테스트 환경**: ✅ 구축 완료
- **디버깅 데이터**: ✅ 수집 및 분석 완료
- **해결 방향**: ✅ 명확한 로드맵 수립

---

**최종 업데이트**: 2025-09-17 23:30 Claude 에이전트 (네트워크 매핑 디버깅 세션)

**다음 검토**: 한국어 netstat 파싱 로직 구현 시작

---

## 🏗️ **Current Architecture Status**

### ✅ **Completed Infrastructure**

- **Namespace Unification**: All `WindowsSentinel` → `LogCheck` conversions complete
- **MonitoringHub**: Singleton pattern for centralized monitoring control
- **ICaptureService/CaptureService**: Packet capture abstraction layer
- **Settings Persistence**: AutoSelectNic, BpfFilter, AutoStartMonitoring
- **Tray Integration**: System tray with balloon notifications
- **Material Design Theme**: Light/Dark theme support
- **Build Status**: ✅ Success (64 warnings, no errors)

### 🔄 **Work In Progress**

- Exception handling improvements
- UI responsiveness optimization
- Data visualization enhancements

---

## 🎯 **Priority Queue (Always Check TODO.md First)**

### 🔴 **Critical (1-2 days)**

1. **OnHubMonitoringStateChanged Exception Fix**

   - File: `NetWorks_New.xaml.cs` line ~232
   - Issue: Null reference and UI thread safety
   - Impact: Application stability

2. **System Idle Process Whitelist**
   - Handle PID 0 process exceptions
   - Exclude from analysis but maintain UI visibility

### 🟠 **High Priority (3-5 days)**

3. **PID Grouping in DataGrid**

   - Group network connections by Process ID
   - Implement CollectionViewSource grouping

4. **System/General Process Differentiation**

   - Visual distinction (color coding)
   - Enhanced warning display for system processes

5. **Risk Level Display Enhancement**
   - Implement `RiskLevelToBackgroundConverter`
   - Replace gray backgrounds with color-coded risk levels

### 🟡 **Medium Priority (1 week)**

6. **Chart Data Improvement**

   - Switch from connection count to data transfer volume
   - Enhanced time-series visualization

7. **MessageBox → Toast Notification**
   - Replace invasive MessageBox dialogs
   - Implement snackbar/toast system

---

## 🛠️ **Working Principles**

### **1. Development Workflow**

```
1. Read TODO.md for current priorities
2. Check DONE.md to understand completed work
3. Follow CODING_STANDARDS.md guidelines
4. Reference monitoring-architecture.md for design decisions
5. Build and test after each significant change
6. Update appropriate .md files with progress
```

### **2. Code Quality Standards**

- **Namespace**: Always use `LogCheck.*` hierarchy
- **Async Methods**: Proper async/await patterns with exception handling
- **Null Safety**: Enable nullable reference types, handle null cases
- **Threading**: UI operations on Dispatcher thread only
- **Logging**: Use structured logging for debugging
- **Performance**: Target <5% CPU, <150MB memory usage

### **3. UI/UX Consistency**

- **Material Design**: Follow MaterialDesignInXAML guidelines
- **Korean Localization**: UI text in Korean, code/comments in English
- **Responsive Design**: Support different screen sizes
- **Accessibility**: Keyboard navigation and screen reader support

---

## 🏛️ **Architecture Compliance**

### **Data Flow (from monitoring-architecture.md)**

```
PacketCapture (SharpPcap)
  → Parser (PacketDotNet)
    → ProcessNetworkMapper
      → RealTimeSecurityAnalyzer
        → UI Updates + SQLite Storage
```

### **Key Interfaces to Implement**

- `ICaptureService`: Packet capture abstraction
- `IProcessMapper`: PID/Port resolution
- `IAnalyzer`: Security rule evaluation
- `IStorage`: Async data persistence

### **Threading Architecture**

- **Capture Thread**: Background packet capture
- **Parser Thread**: Packet parsing and analysis
- **UI Thread**: View updates via Dispatcher
- **Storage Thread**: Async database operations

---

## 🔧 **Current Technical Debt**

### **Warning Reduction Goals**

- **Current**: 64 warnings
- **Target**: <20 warnings
- **Focus Areas**:
  - Null reference warnings (CS8618, CS8600, CS8625)
  - Platform-specific API warnings (CA1416)
  - Unused variable/field warnings (CS0169, CS0414)

### **Performance Optimizations**

- Implement proper backpressure handling
- Add connection pooling for database operations
- Optimize UI virtualization for large datasets

---

## 💾 **Session Memory & Context**

### **Last Completed Task**

- **Date**: 2025-09-17
- **Task**: Complete namespace unification
- **Files Modified**:
  - SecurityAnalyzer.cs, NetworkUsageRecord.cs, SecurityStatusItem.cs
  - StringToVisibilityConverter.cs, Recoverys.xaml.cs, NetWorks.xaml.cs
  - WmiHelper.cs, App.xaml, App.xaml.cs, LogCheck.csproj

### **Current Focus Area**

- Exception handling and application stability
- UI thread safety improvements
- System process handling optimization

### **Next Session Continuation Points**

1. Start with `OnHubMonitoringStateChanged` exception fix
2. Review current error logs and crash reports
3. Implement proper null checking patterns
4. Test with various network conditions

---

## 🚦 **Decision Making Framework**

### **When Making Code Changes**

1. **Consult Architecture**: Does this align with monitoring-architecture.md?
2. **Check Standards**: Does this follow CODING_STANDARDS.md?
3. **Consider Impact**: Will this affect existing functionality?
4. **Test Thoroughly**: Build, run, and verify UI responsiveness
5. **Document Changes**: Update relevant .md files

### **When Prioritizing Tasks**

1. **Stability First**: Fix crashes and exceptions
2. **User Experience**: Improve responsiveness and feedback
3. **Feature Completion**: Complete half-implemented features
4. **Code Quality**: Reduce technical debt
5. **New Features**: Add new functionality last

---

## 🎮 **Quick Commands for Claude**

### **Session Start**

- "Check TODO.md and start the highest priority task"
- "Review current build status and identify next steps"
- "Analyze error logs and plan fixes"

### **During Development**

- "Build and verify the current changes"
- "Update CLAUDE.md with progress made"
- "Apply CODING_STANDARDS.md to current file"

### **Session End**

- "Update TODO.md with completed tasks"
- "Document any new decisions or patterns"
- "Prepare next session continuation point"

---

## 📊 **Success Metrics**

### **Code Quality**

- [ ] Build warnings reduced to <20
- [ ] Zero unhandled exceptions in normal operation
- [ ] All critical UI operations under 100ms response time
- [ ] Memory usage stable under 150MB

### **User Experience**

- [ ] No UI freezing during packet capture
- [ ] Smooth real-time data updates
- [ ] Intuitive error messages and guidance
- [ ] Consistent visual design across all pages

### **Architecture Compliance**

- [ ] All components follow defined interfaces
- [ ] Proper separation of concerns (MVVM)
- [ ] Async operations don't block UI thread
- [ ] Database operations are properly batched

---

## 🔄 **Regular Maintenance Tasks**

### **Daily (Each Session)**

- Check and resolve build warnings
- Review exception logs
- Test critical user workflows
- Update progress documentation

### **Weekly**

- Review and update architecture decisions
- Consolidate code improvements
- Performance benchmarking
- User feedback integration

---

**Last Updated**: 2025-09-17 by Claude Agent  
**Next Review**: When starting next development session
