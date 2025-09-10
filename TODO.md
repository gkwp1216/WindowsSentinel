# 작업 목록 (TODO)

# 예외 오류 발생
    System.NullReferenceException: 'Object reference not set to an instance of an object.'
    network.xaml.cs 357번줄
    
## UI/UX 개선

- **보안 경고 팝업**: '네트워크 보안 모니터링' 기능의 보안 경고 및 권장 조치 메시지를 사용자 친화적인 팝업 형태로 구현합니다.
- **메인 윈도우 재구성**:
- - (완료) 기존의 '악성 프로그램 탐지', '네트워크 접속 내역', '보안 프로그램 복구' 기능을 메인 윈도우에서 분리했습니다.
- (완료) '네트워크 보안 모니터링'을 메인 기능으로 하여 `MainWindow`의 UI를 전면 재설계했습니다.

## 기능 개선

- **상시 네트워크 모니터링**: 사용자가 별도로 '시작' 버튼을 누르지 않아도 프로그램 실행 시 네트워크 모니터링이 자동으로 시작되도록 백그라운드에서 상시 실행되는 기능을 구현합니다.
- 백그라운드에서 작동 시(시스템 트레이로 보낼 시) 모니터링이 작동하지 않는 문제 해결
- 프로세스 종료와 네트워크 연결을 끊을 수 있도록 수정

## 코드 품질 및 아키텍쳐
- UpdateProcessNetworkDataAsync 메서드 내 Dispatcher.InvokeAsync
    -비동기 작업(Task.Run)이 완료된 후 결과만 UI 스레드로 보내는 패턴이 더 효율적
    -개선방안
        -개선 방안: Task.Run 안에서 데이터 처리와 보안 분석(_securityAnalyzer.AnalyzeConnectionsAsync(data))을 모두 수행하고, 최종적으로 UI 업데이트에 필요한 부분(_processNetworkData.Clear(), _securityAlerts.Clear())만 Dispatcher.InvokeAsync로 호출하는 방식으로 리팩토링

- 통계 데이터 업데이트 방식: UpdateStatistics와 UpdateStatisticsDisplay를 분리한 것은 좋으나, _totalConnections, _lowRiskCount와 같은 필드들을 직접 업데이트하고 있다. 이는 WPF의 MVVM(Model-View-ViewModel) 패턴과 충돌할 수 있다.
    - 개선 방안: 이 통계 데이터들을 별도의 ViewModel 클래스로 분리하고, INotifyPropertyChanged 인터페이스를 구현하여 값이 변경될 때마다 UI가 자동으로 업데이트되도록 바인딩하는 것이 WPF의 정석적인 방법이다. 이렇게 하면 코드의 관심사 분리가 명확해지고 유지보수가 훨씬 쉬워진다.

- 코드 중복: StartMonitoring_Click과 Refresh_Click에서 _processNetworkMapper.GetProcessNetworkDataAsync() 호출과 UpdateProcessNetworkDataAsync 호출이 중복

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

### 우선순위 작업 (남음)

- 보안 경고 팝업 구현: 보안 이벤트 발생 시 표시될 팝업 컴포넌트 설계 및 경고 레벨별 UX 흐름 정의
- 상시 네트워크 모니터링: 애플리케이션 시작 시 네트워크 캡처 및 분석 서비스 자동 시작 (서비스/스레드 초기화, 예외 처리 계획)
- UI 추가 개선 진행