# AbuseIPDB AutoBlock 테스트 시스템

실제 악성 IP를 이용한 AutoBlock 기능 테스트 도구입니다.

## 🎯 개요

기존의 `telnet 8.8.8.8 1337` 같은 단순한 연결 테스트는 AutoBlock 시스템을 제대로 검증하기 어려웠습니다. 이 시스템은 **AbuseIPDB**의 실제 악성 IP 데이터를 활용하여 보다 현실적인 AutoBlock 테스트를 제공합니다.

## 🔧 구성 요소

### 1. AbuseIPTestService.cs

- **위치**: `LogCheck\Services\AbuseIPTestService.cs`
- **기능**: AbuseIPDB API 연동 및 악성 IP 조회
- **특징**:
  - API 키 없이도 알려진 악성 IP 사용 가능
  - 실시간 위험도 정보 제공
  - HTTP 클라이언트 자동 정리

### 2. AutoBlockTestApp (콘솔 애플리케이션)

- **위치**: `AutoBlockTestApp\Program.cs`
- **기능**: 독립실행형 테스트 도구
- **테스트 옵션**:
  1. 의심스러운 포트 연결 테스트
  2. 지속적인 HTTP 요청 테스트
  3. Raw Socket 연결 테스트
  4. **AbuseIPDB 의심스러운 IP 테스트** ⭐

### 3. UI 통합 (NetWorks_New.xaml)

- **기능**: 메인 애플리케이션에서 직접 테스트 실행
- **위치**: "AutoBlock 테스트" 버튼
- **특징**:
  - 실시간 진행 상황 표시
  - 통계 연동으로 차단 결과 확인
  - 안전 확인 다이얼로그

### 4. PowerShell 스크립트

- **위치**: `Test-AbuseIPDB-AutoBlock.ps1`
- **기능**: 명령줄에서 독립 실행
- **사용법**:
  ```powershell
  .\Test-AbuseIPDB-AutoBlock.ps1 -ApiKey "YOUR_API_KEY" -MaxIPs 3
  ```

## 🚀 사용 방법

### UI에서 테스트

1. **LogCheck 실행**
2. **네트워크 모니터링 시작** ("시작" 버튼 클릭)
3. **"AutoBlock 테스트"** 버튼 클릭
4. 안전 확인 다이얼로그에서 **"예"** 선택
5. 테스트 진행 상황을 실시간으로 확인
6. 완료 후 통계 패널에서 차단 결과 확인

### 콘솔 애플리케이션 사용

1. **AutoBlockTestApp.exe** 실행
2. **"4. AbuseIPDB 의심스러운 IP 테스트"** 선택
3. 자동으로 악성 IP 목록 조회 및 테스트 실행

### PowerShell 스크립트 사용

```powershell
# 기본 실행 (API 키 없음)
.\Test-AbuseIPDB-AutoBlock.ps1

# API 키 사용
.\Test-AbuseIPDB-AutoBlock.ps1 -ApiKey "YOUR_ABUSEIPDB_API_KEY" -MaxIPs 5

# 지연 시간 조정
.\Test-AbuseIPDB-AutoBlock.ps1 -DelaySeconds 5
```

## 🛡️ 안전 고려사항

⚠️ **주의사항**

- 실제 악성 IP에 연결을 시도하는 테스트입니다
- 네트워크 관리자의 승인을 받고 실행하세요
- 회사/조직의 보안 정책을 확인하세요
- 테스트 후 방화벽 로그를 확인하세요

## 📊 테스트 결과 확인

### 1. 실시간 로그

- UI 하단의 로그 패널에서 실시간 진행 상황 확인
- 연결 성공/실패 상태 모니터링

### 2. AutoBlock 통계

- 차단된 연결 수 증가 확인
- IP별 차단 통계 조회
- 일일/주간/월간 통계 비교

### 3. 데이터베이스 로그

```sql
-- AutoBlock 통계 조회
SELECT * FROM AutoBlockDailyStats WHERE Date = date('now');
SELECT * FROM IPBlockStats ORDER BY BlockCount DESC;
SELECT * FROM ProcessBlockStats ORDER BY BlockCount DESC;
```

## 🔍 테스트 IP 목록

기본 제공되는 알려진 악성 IP:

- `185.220.70.8` - Tor exit node
- `45.95.169.157` - Known malicious
- `198.98.60.19` - Suspicious activity
- `89.248.165.2` - Bot network
- `104.248.144.120` - Malicious host

## 🌐 AbuseIPDB API 연동

### API 키 획득

1. [AbuseIPDB](https://www.abuseipdb.com/) 가입
2. API 키 발급 (무료 계정: 1일 1,000회 요청)
3. 테스트 시 API 키 입력

### API 없이 사용

- API 키 없이도 알려진 악성 IP로 테스트 가능
- 제한된 IP 목록이지만 기본적인 테스트 수행 가능

## 🔧 문제 해결

### AutoBlock이 작동하지 않는 경우

1. **패킷 캡처 확인**

   - 관리자 권한으로 실행했는지 확인
   - 네트워크 인터페이스가 올바르게 선택되었는지 확인

2. **방화벽 설정 확인**

   - Windows 방화벽이 활성화되어 있는지 확인
   - 차단 규칙이 생성되는지 확인

3. **네트워크 연결 확인**
   - 실제로 외부 연결이 가능한 환경인지 확인
   - 프록시나 VPN이 간섭하지 않는지 확인

### 테스트 연결이 실패하는 경우

1. **타임아웃 늘리기**

   - 네트워크 지연이 큰 환경에서는 타임아웃 시간 증가

2. **다른 포트 시도**

   - 일부 포트가 ISP 수준에서 차단될 수 있음
   - 다양한 포트로 테스트 실행

3. **방화벽 허용 규칙**
   - 테스트 중에는 일시적으로 방화벽 설정 조정

## 📝 로그 예시

```
🔍 AbuseIPDB AutoBlock 테스트 시작...
📡 AbuseIPDB에서 의심스러운 IP 목록 조회 중...
🎯 테스트 대상 IP: 185.220.70.8, 45.95.169.157, 198.98.60.19
🔄 185.220.70.8 연결 테스트 시작
📊 185.220.70.8 - 위험도: 85%, 국가: DE
🔌 연결 시도: 185.220.70.8:80
✅ 연결 성공: 185.220.70.8:80
📦 응답 수신: 512 bytes
✅ 185.220.70.8 테스트로 3개 연결이 차단되었습니다!
🎉 AbuseIPDB AutoBlock 테스트 완료!
```

## 🔄 업데이트 내역

### v1.0.0

- AbuseIPDB API 연동 구현
- UI 버튼 추가
- PowerShell 스크립트 생성
- 콘솔 테스트 앱 구현
- 통계 시스템 연동
