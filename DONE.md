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
  - 상단 우측 설정 진입 버튼을 기어 아이콘(MaterialDesign PackIcon Cog)으로 변경
  - 액션 영역을 WrapPanel로 구성해 좁은 폭에서도 버튼이 잘리지 않도록 개선
  - Setting 화면 좌측 상단에 뒤로가기(ArrowLeft) 버튼 추가: NetWorks_New로 복귀 경로 확보
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

## 향후 개선 제안(선택)

- 단축키 추가: 전역 Ctrl+, 으로 설정 열기
- 테마 대비/가독성 미세 조정(라이트/다크)
- 경고 정리: CS8618/CS8600/CS8622 및 분석 경고 선별적 해소
- 최소 단위 테스트: 설정 저장/로드, BPF 검증 로직 등 핵심 경로 스모크 테스트

# - 자식 프로세스에도 프로세스명 표시 (줄맞춤)

# System Idle Process 분석 예외 처리 / System Idle Process 화이트리스트 처리

# - 같은 PID는 묶어서 표시되도록 수정

# Windows Task Manager 방식으로 그룹 확장 상태 유지 문제 해결 (DataGrid → TreeView)

- ProcessTreeNode 모델: INotifyPropertyChanged와 정적 Dictionary를 통한 전역 상태 관리
- TreeView 구조: DataGrid 그룹핑을 완전히 TreeView로 교체, HierarchicalDataTemplate 적용
- 스마트 업데이트: 기존 노드 재사용으로 UI 파괴 없이 데이터만 업데이트
- 자동 상태 관리: TwoWay 데이터 바인딩으로 수동 복원 로직 불필요
- 이벤트 마이그레이션: 모든 이벤트 핸들러를 TreeView 방식으로 완전 전환
