# DONE

## 핵심 완료 사항

- 모니터링 인프라 정비 및 자동화

  - ICaptureService/CaptureService 도입, RunLoop 비동기 실행 구조 정리
  - MonitoringHub 싱글톤으로 캡처 라이프사이클/이벤트 중앙화
  - 앱 시작 시 자동 모니터링(설정 기반)과 트레이 메뉴 토글로 시작/중지 제어
  - 시작/중지/자동시작 시 트레이 풍선 알림으로 사용자 피드백 제공

- 설정(네트워크) 기능 구현 및 저장

  - Settings: AutoSelectNic, SelectedNicId, BpfFilter 추가 (저장/로드)
  - NIC 나열/선택 및 BPF 필터 검증/저장 로직(SharpPcap 장치 활용)
  - 설정 변경 사항이 다음 실행에 반영되도록 지속성 보장

- UI/UX 가시성 및 네비게이션 개선

  - NetWorks_New 화면에 런타임 요약(NIC, BPF) 상시 표시 및 모니터링 상태 변동 시 갱신
  - 액션 영역을 WrapPanel로 구성해 좁은 폭에서도 버튼이 잘리지 않도록 개선
  - 트레이 메뉴에 “설정” 항목 추가: 메인 창 표시 후 설정 페이지로 이동

- 빌드/XAML 안정화

  - WPF 빌드 오류(MC3072: Spacing 속성 미지원) 해결 → Margin 기반 레이아웃으로 교체
  - MaterialDesign PackIcon 사용을 위한 XAML 네임스페이스 누락(MC3000) 보완
  - Windows 전용 API 경고(CA1416) 및 일부 널 가능성 경고는 정보성으로 유지, 빌드 성공 상태 확보

## 참고: 구조/파일 추가 및 문서

- Services
  - ICaptureService, CaptureService: StartAsync에서 캡처 루프를 별도 Task로 실행하도록 정리
- Models
  - PacketDto, FlowRecord, SecurityAlert 등 데이터 모델 추가
- docs
  - MonitoringArchitecture.md: 아키텍처 개요 초안

# - 자식 프로세스에도 프로세스명 표시 (줄맞춤)

## AutoBlock 자동 차단 시스템 구축 완료 (2025.09.28)

### 핵심 인프라 구현

- **IAutoBlockService 인터페이스 설계 및 구현**

  - 자동 차단 서비스의 핵심 계약 정의
  - 비동기 패턴 완전 지원 (AnalyzeConnectionAsync, BlockConnectionAsync)
  - 화이트리스트 관리 시스템 (AddToWhitelistAsync, RemoveFromWhitelistAsync)
  - 통계 조회 및 차단 이력 관리 기능

- **BlockRuleEngine 3단계 규칙 평가 시스템**

  - Rules.md 기반 체계적인 위협 분석 엔진
  - Level 1 (Immediate): 알려진 악성 IP/포트, System Idle Process 위장 탐지
  - Level 2 (Warning): 의심스러운 네트워크 패턴, 비표준 포트 사용
  - Level 3 (Monitor): 새로운 프로그램 활동, 주기적 통신 패턴

- **AutoBlockService 완전 구현**
  - SQLite 데이터베이스 통합 (autoblock.db)
  - BlockedConnections, Whitelist 테이블 자동 생성
  - 실시간 방화벽 제어 (netsh advfirewall 명령)
  - 프로세스 종료 및 연결 차단 기능
  - 통계 수집 및 히스토리 추적

### 데이터베이스 스키마 설계

- **BlockedConnections 테이블**

  - 차단된 연결의 모든 세부 정보 저장
  - 프로세스 정보, 네트워크 정보, 차단 레벨, 신뢰도 점수
  - 트리거된 규칙 목록 및 위협 카테고리 분류

- **Whitelist 테이블**
  - 화이트리스트 항목 관리 (프로세스 경로 기반)
  - 추가 사유, 만료 시간, 활성화 상태 관리
  - 사용자별 및 시스템 화이트리스트 구분

### 테스트 프레임워크 구축 (추후 삭제 고려)

- 다양한 테스트 시나리오 (정상/악성/의심스러운 연결)
- 성능 벤치마크 및 부하 테스트 도구
- 실제 위협 시뮬레이션 및 검증 기능

### 시스템 아키텍처 완성도

- **3단계 자동 차단 시스템 완전 가동**

  - 실시간 위협 탐지 및 즉시 대응
  - 규칙 기반 체계적 분석
  - 사용자 개입 최소화된 자동화

- **확장 가능한 구조 설계**
  - 인터페이스 기반 모듈화
  - 새로운 차단 규칙 추가 용이성
  - 데이터베이스 스키마 확장성

### 시스템 아키텍처 완성도

- **3단계 자동 차단 시스템 완전 가동**

  - 실시간 위협 탐지 및 즉시 대응
  - 규칙 기반 체계적 분석
  - 사용자 개입 최소화된 자동화

- **확장 가능한 구조 설계**
  - 인터페이스 기반 모듈화
  - 새로운 차단 규칙 추가 용이성
  - 데이터베이스 스키마 확장성

### 🎯 AutoBlock UI 통합 완료 (2025-09-29)

- **실시간 데이터 바인딩 구현 ✅**

  - TotalBlockedCount, Level1/2/3BlockCount 통계 프로퍼티 완성
  - BlockedConnections, WhitelistEntries Observable 컬렉션 연결
  - LoadAutoBlockDataAsync() 및 UpdateAutoBlockStatistics() 메서드 구현

- **사용자 인터랙션 기능 ✅**

  - 차단된 연결 상세 정보 표시 DataGrid 구현
  - 실시간 AutoBlock 서비스 상태 모니터링 활성화

- **AutoBlock UI 가시성 문제 해결 완료 ✅**
  - XAML 컨트롤 이름 일관성 확보 (WhitelistDataGrid 연결 검증)
  - 이벤트 핸들러와 UI 요소 간 정확한 연결 완료

### 주요 완료 사항 (2025.09.28)

- **AutoBlock 자동 차단 시스템 완전 구현**
- **NetWorks_New.xaml.cs 메인 UI 통합**
- **Rules.md 기반 3단계 차단 규칙 시스템**
- **SQLite 데이터베이스 통합 및 실시간 통계**
- **보안 알림 시스템 완전 연동**
