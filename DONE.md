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

## 주요 화면 변경 사항

- NetWorks_New

  - 상태 영역에 "NIC: … | BPF: …" 요약 텍스트 추가
  - 모니터링 시작/중지 등 상태 이벤트 시 요약 텍스트 갱신
  - 우측 상단 기어 아이콘 버튼으로 설정 페이지 이동(툴팁 제공)

- Setting

  - 테마(라이트/다크), 자동 시작, NIC 자동선택/수동 선택, BPF 필터 검증/저장 제공
  - 좌측 상단 뒤로가기 버튼으로 NetWorks_New로 복귀

- Tray(시스템 트레이)
  - 모니터링 시작/중지 토글 및 풍선 알림
  - “설정” 메뉴 항목으로 즉시 설정 페이지 진입 가능

## 동작 요약

- 앱 시작 시 설정값(자동 시작, NIC/BPF)에 따라 캡처 자동 시작 가능
- 설정 변경은 저장 후 다음 실행에 반영되며, 런타임 요약을 통해 가시적으로 확인 가능
- 시작/중지 시 트레이 풍선 알림으로 상태 변화를 즉시 인지 가능

## 빌드/실행

- VS Code 작업(Task)
  - Build: 솔루션 빌드 (현재 경고 다수 존재하나 기능 영향 없음)
  - Publish: 솔루션 퍼블리시
  - Watch: 변경 감지 실행(개발 중 편의)

## 기술 스택

- .NET 8 WPF
- SharpPcap / PacketDotNet (패킷 캡처/해석)
- MaterialDesignInXaml (아이콘/스타일), LiveChartsCore (차트)

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

## 현재 상태 메모

- MaterialDesign 네임스페이스 누락으로 인한 XAML 파싱 오류(MC3000) 해결됨
- 설정 진입 경로(기어 아이콘, 트레이 메뉴)와 뒤로가기 네비게이션 정상 동작
- 빌드 성공, 기능 동작 확인(경고는 정보성으로 유지)

# System Idle Process 분석 예외 처리 / System Idle Process 화이트리스트 처리

# - 같은 PID는 묶어서 표시되도록 수정
