# 작업 목록 (TODO)

## 코드 품질 및 아키텍쳐

- UpdateProcessNetworkDataAsync 메서드 내 Dispatcher.InvokeAsync -비동기 작업(Task.Run)이 완료된 후 결과만 UI 스레드로 보내는 패턴이 더 효율적 -개선방안 -개선 방안: Task.Run 안에서 데이터 처리와 보안 분석(\_securityAnalyzer.AnalyzeConnectionsAsync(data))을 모두 수행하고, 최종적으로 UI 업데이트에 필요한 부분(\_processNetworkData.Clear(), \_securityAlerts.Clear())만 Dispatcher.InvokeAsync로 호출하는 방식으로 리팩토링

- 통계 데이터 업데이트 방식: UpdateStatistics와 UpdateStatisticsDisplay를 분리한 것은 좋으나, \_totalConnections, \_lowRiskCount와 같은 필드들을 직접 업데이트하고 있음.
  이는 WPF의 MVVM(Model-View-ViewModel) 패턴과 충돌할 수 있다.

  - 개선 방안: 이 통계 데이터들을 별도의 ViewModel 클래스로 분리하고, INotifyPropertyChanged 인터페이스를 구현하여 값이 변경될 때마다 UI가 자동으로 업데이트되도록 바인딩하는 것이 WPF의 정석적인 방법이다. 이렇게 하면 코드의 관심사 분리가 명확해지고 유지보수가 훨씬 쉬워진다.

- 코드 중복: StartMonitoring_Click과 Refresh_Click에서 \_processNetworkMapper.GetProcessNetworkDataAsync() 호출과 UpdateProcessNetworkDataAsync 호출이 중복

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

#### System Idle Process에 대하여

# System Idle Process 란? - **'CPU의 유휴 자원(여유 시간)이 얼마나 되는가'** 를 보여주는 수치 (CPU가 얼마나 놀고있는가)

# 왜 쓰는가?

    CPU 스케줄러의 안정성을 위해서. CPU는 항상 무언가 처리할 준비가 되어 있어야 하는데, 만약 처리할 작업이 정말 하나도 없으면 시스템이 불안정해질 수 있음.
    System Idle Process는 "할 일이 없을 때 하는 일"이라는 명목으로 항상 대기, 시스템이 안정적으로 유지되도록 돕는다. 또한, CPU가 쉬는 시간을 측정하여 전력 소모를 줄이는 역할도 담당.

# 개발 관점을 고려한 System Idle Process

# System Idle Process 분석 예외 처리

    System Idle Process는  PID(프로세스 식별자)가 0으로 고정된, 커널 수준의 특수 프로세스.
    따라서 WS가 비정상적인 자원 사용량을 감시 or 악성 행위를 탐지할 때, 이 프로세스는 반드시 예외(화이트리스트) 처리해야 함

# System Idle Process 유사 이름 악성코드 탐지

    공격자들은 이 프로세스의 이름을 교묘하게 위장하여 악성코드를 숨기는 경우가 종종 있음. 예) System ldle Process (Idle의 I가 소문자 L)  or System Idle Process.exe (정상 프로세스는 .exe 확장자가 없음)
    프로세스의 실행 경로, 디지털 서명, PID 등을 종합적으로 분석하여 이러한 위장 악성코드를 정확히 걸러낼 수 있는 로직을 갖추어야 함

### 우선순위 작업 (TODO)

- 이상 네트워크 차단 규칙 수립
- 차단된 네트워크 리스트를 표시하는 UI 제작
- System Idle Process 화이트리스트화 (분석 예외) 및 숨김 처리
- 보안 경고 팝업 구현: 보안 이벤트 발생 시 표시될 팝업 컴포넌트 설계 및 경고 레벨별 UX 흐름 정의
