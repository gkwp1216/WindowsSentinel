# AutoBlock 차단 기능 테스트 가이드

## 🧪 테스트 방법

### 1. UI에서 직접 테스트

NetWorks_New 페이지의 AutoBlock 탭에서 **🧪 AutoBlock 테스트** 버튼을 클릭하세요.

**테스트 내용:**

- System Idle Process 위장 탐지
- 의심스러운 포트 연결 탐지
- 정상 연결 허용 확인

### 2. 콘솔 애플리케이션으로 테스트

```bash
cd c:\My_Project\WS_git\AutoBlockTester
dotnet run
```

### 3. 실제 네트워크 연결 테스트

#### A. Telnet을 이용한 포트 연결 테스트

```bash
# 의심스러운 포트로 연결 시도 (차단되어야 함)
telnet 8.8.8.8 1337
telnet 1.1.1.1 31337
telnet google.com 12345

# 정상 포트로 연결 시도 (허용되어야 함)
telnet google.com 80
telnet google.com 443
```

#### B. PowerShell을 이용한 연결 테스트

```powershell
# 의심스러운 연결 시도
Test-NetConnection -ComputerName "8.8.8.8" -Port 1337
Test-NetConnection -ComputerName "1.1.1.1" -Port 31337

# 정상 연결 확인
Test-NetConnection -ComputerName "google.com" -Port 443
```

#### C. 가짜 악성 프로세스 시뮬레이션

```bash
# 의심스러운 이름의 실행 파일 생성 후 네트워크 연결 시도
copy notepad.exe "System Idle Process.exe"
# 이 파일을 실행하면 AutoBlock에서 탐지해야 함
```

### 4. 화이트리스트 테스트

1. 정상적인 프로그램을 차단 목록에 추가
2. 해당 프로그램을 화이트리스트에 추가
3. 재연결 시도 시 허용되는지 확인

### 5. 성능 테스트

```bash
cd c:\My_Project\WS_git\AutoBlockTester
dotnet run
# 성능 테스트 선택 (Y)
# 연결 수 입력 (예: 1000)
```

## 📊 테스트 결과 확인

1. **UI 로그**: NetWorks_New 페이지 하단의 로그 메시지 확인
2. **AutoBlock 탭**: 차단된 연결 목록에서 실시간 차단 내역 확인
3. **데이터베이스**: `autoblock.db` 파일의 `BlockedConnections` 테이블 확인
4. **Windows 방화벽**: `netsh advfirewall firewall show rule name=all` 명령으로 차단 규칙 확인

## ⚠️ 주의사항

- **관리자 권한 필요**: 실제 차단 기능 테스트 시 관리자 권한으로 실행
- **방화벽 규칙**: 테스트 후 불필요한 방화벽 규칙 정리 필요
- **화이트리스트**: 테스트용 화이트리스트 항목 정리 필요

## 🔧 트러블슈팅

### 테스트가 작동하지 않는 경우:

1. 관리자 권한으로 실행했는지 확인
2. AutoBlock 서비스가 초기화되었는지 확인
3. 데이터베이스 연결 상태 확인
4. 로그 메시지에서 오류 내용 확인

### 실제 차단이 되지 않는 경우:

1. Windows 방화벽이 활성화되어 있는지 확인
2. `netsh advfirewall` 명령 실행 권한 확인
3. 프로세스 종료 권한 확인
