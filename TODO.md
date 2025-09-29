# 작업 목록 (TODO)

## 성능 최적화 계획 📋

> **📄 상세 계획서**: [PERFORMANCE_OPTIMIZATION_PLAN.md](./PERFORMANCE_OPTIMIZATION_PLAN.md) 참조  
> **🗺️ 실행 로드맵**: [PERFORMANCE_OPTIMIZATION_ROADMAP.md](./PERFORMANCE_OPTIMIZATION_ROADMAP.md) 참조

### Phase 1: 핵심 시스템 개발 (완료) ✅

- ✅ AutoBlock 서비스 아키텍처 설계
- ✅ IAutoBlockService 인터페이스 정의
- ✅ BlockRuleEngine 구현
- ✅ AutoBlockService 핵심 기능
- ✅ 데이터베이스 스키마 및 Repository 패턴
- ✅ 실시간 패킷 분석 및 자동 차단
- ✅ 화이트리스트 관리 시스템
- ✅ 테스트 프레임워크 구성
- ✅ UI 통합 작업 (AutoBlock 탭 및 통계 표시)
- ✅ 자식 프로세스만 연결 차단할 경우 부모 프로세스까지 프로그램 리스트에 표시되지 않으며 AutoBlock 시스템과 차단된 연결 모두에서 차단 기록/내역이 표시되지 않는 문제 해결

### Phase 2: 성능 최적화 (진행 예정) 🚀

#### 즉시 개선 작업 (Week 1-2)

- [ ] **UI 스레드 최적화**: UpdateTimer_Tick 비동기 패턴 개선
- [ ] **메모리 사용량 개선**: 증분 업데이트로 ObservableCollection 최적화
- [ ] **DB 쓰기 성능**: AutoBlock 배치 처리 구현

#### 구조적 개선 (Week 3-5)

- [ ] **캐싱 시스템**: WMI 쿼리 결과 캐시 구현
- [ ] **MVVM 패턴 완성**: ViewModel 분리 및 데이터 바인딩 강화
- [ ] **BackgroundService**: 백그라운드 처리 패턴 적용

#### 고급 최적화 (Week 6-9)

- [ ] **객체 풀링**: ProcessNetworkInfo 객체 재사용
- [ ] **성능 모니터링**: 실시간 메트릭 수집 시스템
- [ ] **연결 풀**: 데이터베이스 연결 최적화

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

### 우선순위 작업 (TODO)

- 안되는 기능들 작동하게끔 작업 ( 그래프는 잘 안되는데? )
- 시스템 프로세스는 추가 경고 표시 및 추가 정렬 (색상 추가, 따로 분리 ) // 생각해볼것
- 보안 경고 팝업 구현: 보안 이벤트 발생 시 표시될 팝업 컴포넌트 설계 및 경고 레벨별 UX 흐름 정의

### 우선순위 작업 (TODO)

- **AutoBlock UI 개선**: 차단된 연결 상세 정보 표시 페이지
- **차단 규칙 커스터마이징**: 사용자 정의 차단 규칙 추가 기능
- **성능 모니터링**: AutoBlock 시스템 성능 지표 및 최적화
- **ftp , tftp?** : AutoBlock 기능 테스트를 위한 방법 연구
- ✅ 자식 프로세스만 연결 차단할 경우 부모 프로세스까지 프로그램 리스트에 표시되지 않으며 AutoBlock 시스템과 차단된 연결 모두에서 차단 기록/내역이 표시되지 않는 문제 해결
- [ ] **AutoBlock 시스템 통합**: AutoBlock 시스템과 차단된 연결을 통합 관리하는 방법 연구
- [ ] **차단 범위 최적화**: 그룹화 프로세스 vs 상세 프로세스 차단 전략 수립

# autoblock.db 경로

\WS\LogCheck\bin\Debug\net8.0-windows
