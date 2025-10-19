# 📁 Demo_Scripts 폴더

이 폴더에는 WindowsSentinel 데모 및 테스트를 위한 스크립트와 가이드 문서가 포함되어 있습니다.

---

## 📋 파일 목록

### 🔴 공격 생성 스크립트

#### 1. `Simple_Attack_Generator.ps1`

- **설명**: TCP SYN Flood 공격 시뮬레이션 스크립트
- **용도**: WindowsSentinel의 DDoS 탐지 기능 테스트
- **대상**: 127.0.0.1 (로컬호스트)
- **포트**: 80, 443, 8080, 3389, 445, 135, 139
- **강도**: 100 packets/sec (조절 가능)
- **지속 시간**: 30초

**사용법:**

```powershell
# 관리자 권한 PowerShell에서 실행
cd C:\My_Project\WS\Demo_Scripts
.\Simple_Attack_Generator.ps1
```

---

#### 2. `UDP_Flood_Generator.ps1`

- **설명**: UDP Flood 공격 시뮬레이션 스크립트
- **용도**: UDP 기반 DDoS 탐지 테스트
- **대상**: 127.0.0.1
- **포트**: 53, 123, 161, 500, 1900, 5060 (DNS, NTP, SNMP, IKE, SSDP, SIP)
- **강도**: 200 packets/sec
- **페이로드**: 512 bytes/packet

**사용법:**

```powershell
# 관리자 권한 PowerShell에서 실행
cd C:\My_Project\WS\Demo_Scripts
.\UDP_Flood_Generator.ps1
```

---

### 📚 가이드 문서

#### 3. `데모_완벽_가이드_실제트래픽버전.md`

- **설명**: 완벽한 데모 준비 및 실행 가이드
- **내용**:
  - Attack_Simulator 문제점 분석
  - 실제 트래픽 생성 방법
  - 단계별 데모 시연 절차
  - 발표 시나리오 및 타임라인
  - 트러블슈팅 가이드
  - 예상 질문 & 답변
  - 발표 준비 체크리스트

#### 4. `데모_테스트_가이드.md`

- **설명**: 간단한 테스트 가이드
- **내용**:
  - 기본 설정 방법
  - 테스트 시나리오
  - 예상 결과

---

## ⚠️ 중요 사항

### 관리자 권한 필수

모든 스크립트는 **관리자 권한**으로 실행해야 합니다:

```powershell
Start-Process powershell -Verb RunAs
```

### 데모 모드 활성화

WindowsSentinel에서 테스트하기 전에 반드시:

1. WindowsSentinel 실행
2. Setting 페이지로 이동
3. **"데모 모드 (사설 IP 공격 탐지 활성화)"** 체크박스 활성화
4. 확인 메시지 확인

### 실행 순서

```
1. WindowsSentinel 실행
2. 데모 모드 활성화
3. 공격 스크립트 실행
4. WindowsSentinel에서 탐지 확인
5. 테스트 완료 후 데모 모드 비활성화
```

---

## 📊 예상 결과

스크립트 실행 후 10-20초 이내:

✅ **Toast 알림**

- "보안 위협 탐지!" 메시지 표시
- 빨간색 Security 타입 알림

✅ **SecurityDashboard**

- 실시간 차트 급증
- 위협 탐지 횟수 증가
- 차단된 IP 표시

✅ **AutoBlock**

- 127.0.0.1 자동 차단
- 차단 사유 기록
- Windows Firewall 규칙 생성

✅ **Logs**

- 상세 탐지 로그 기록
- 타임스탬프, 소스 IP, 공격 유형 표시

---

## 🔧 트러블슈팅

### 문제: "스크립트를 로드할 수 없습니다" 오류

**해결:**

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### 문제: "액세스가 거부되었습니다"

**해결:** 관리자 권한으로 PowerShell 재실행

### 문제: WindowsSentinel이 탐지하지 못함

**해결:**

1. 데모 모드 활성화 여부 확인
2. WindowsSentinel이 실행 중인지 확인
3. NetWorks 탭에서 모니터링이 시작되었는지 확인

---

## 📞 문의

- **Repository**: https://github.com/gkwp1216/WindowsSentinel
- **Issue**: GitHub Issues 페이지에서 문제 보고

---

**마지막 업데이트**: 2025년 10월 19일
