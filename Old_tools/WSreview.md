## WindowsSentinel(LogCheck) 프로젝트 리뷰

### 개요
- 목표: Windows 보안 상태 점검/복구, 네트워크 모니터링, 설치 프로그램 점검, 이벤트 로그 열람을 단일 WPF 앱으로 제공
- 인상: 기능 폭이 넓고 폴백 전략이 좋으나, 권한 처리/정확도/유지보수성/위험 작업 가드 개선 필요

### 기술 스택/구성
- 플랫폼: .NET 8 WPF
- 라이브러리: MaterialDesign, LiveCharts, SharpPcap/PacketDotNet, Microsoft.Data.Sqlite(미사용 추정)
- 테마: Light/Dark + Material

### 주요 기능
- 대시보드: Defender/Firewall/Security Center/BitLocker 상태표시, 실시간/지표 텍스트, 가이드 오버레이
- 네트워크(NetWorks): Npcap 캡처 + 대안(이벤트 로그/netstat/WMI), 필터/히스토리
- 설치 프로그램(ProgramsList): 레지스트리 수집, 서명 확인, 간이 점수, MalwareBazaar 해시 조회
- Vaccine: 파일/설치 경로 해시 검사(MalwareBazaar)
- Logs: 보안 이벤트 로그 뷰 + 한/영 패턴 분석
- Recoverys: PowerShell 기반 보안 복구/최적화
- Setting: 라이트/다크 테마 전환

### 아키텍처/코드 구조
- 네비게이션: `MainWindows.NavigateToPage`로 Frame 교체, 각 페이지 사이드바 중복 구현
- 서비스/유틸: `WmiHelper`(WMI/COM/Registry/PS 혼합), `SecurityAnalyzer`(의심 포트/패턴/통계), `LogHelper`
- 파일 과밀: `NetWorks.xaml.cs` 단일 파일 비대(2.8K+ 라인)
- 모델 중복: `ProgramInfo`가 서로 다른 위치에 중복 정의
- 잔존 참조: 일부 `WindowsSentinel` 네임스페이스 흔적

### 보안/안정성 리스크
- 권한 처리: 페이지 생성자에서 관리자 미보유 시 앱 전체 종료 → 권장 UX 아님
- 위험 작업: BitLocker 활성화/보호기 추가, 방화벽/Defender 정책 강제 변경 시 동의/가드/롤백 안내 부족
- 설치 프로그램 수집: `Win32_Product` 사용(느림/재구성 트리거) → 사용 중단 권장
- 외부 API: MalwareBazaar 다량 호출 시 레이트 리밋 위험, 캐시/배치 없음

### 상태 판별 정확도 개선
- Defender: SecurityCenter2 제품 존재 여부만으로 활성 판단 → `Get-MpComputerStatus`/`MSFT_MpPreference`로 강화
- Firewall: 구형 COM(`HNetCfg.FwMgr`) 대신 `Get-NetFirewallProfile`/`MSFT_NetFirewallProfile`로 일관화
- BitLocker: `EncryptableVolume` 존재 유무 대신 `Get-BitLockerVolume`의 `ProtectionStatus`/`VolumeStatus` 확인

### 성능/UX
- 인위 지연: `Thread.Sleep(5000)` 제거
- 로그 조회: 페이징/기간 필터/비동기 바인딩 적용
- 로깅 I/O: 동기 `File.AppendAllText` 빈번 → 버퍼링/비동기화

### 코드 품질/유지보수성
- MVVM 부재: 캡처/파싱/상태/뷰 로직 분리 필요
- 중복 UI: 사이드바/버튼/네비게이션 공통화(UserControl/스타일)
- 테마 전환: `MergedDictionaries.Clear()` 사용으로 공용 리소스 유실 가능 → 테마 딕셔너리만 교체
- 불필요 항목: 미사용 패키지/설정 정리

### 외부 API/네트워크
- MalwareBazaar: 캐시/동시성 제한/백오프 필요
- SharpPcap: 장치 접근 테스트 간소화, 과도 로깅 축소

### 로깅/진단
- 로그: 일자별 파일, 예외 포함. 사용자 메시지박스 남용은 지양

## 권장 개선(우선순위)
1) 권한 처리 UX 개선: 시작 시 1회 권한 체크/재실행 유도, 페이지 `Shutdown()` 제거 후 기능 비활성/안내
2) 설치 프로그램 집계 교체: `Win32_Product` 제거, 레지스트리 기반 집계/카운트
3) 테마 전환 안정화: 테마 리소스만 스왑, Material/공용 리소스 유지
4) 상태 판별 정밀화: Defender/Firewall/BitLocker를 최신 WMI/PS로 재구현, 폴백 정리
5) 구조 리팩터링(MVVM): `NetWorks` 분리, 사이드바 공통화, `ProgramInfo` 단일화
6) 성능/UX: 인위 지연 제거, 로그/히스토리 페이징/기본 필터
7) 외부 API: 해시 캐시/레이트 리밋 제어
8) 로깅: 비동기화/레벨화/설정화

## 빠른 체감 3가지(바로 적용)
- 페이지 생성자 종료 로직 제거
- 설치 프로그램 카운트 경량화(레지스트리)
- 테마 교체 로직 수정(리소스 유지)

## 소규모 정정/클린업
- `using System.Windows.Data;` 중복 제거
- csproj 미사용 옵션/패키지 정리, 네임스페이스 일관화
- 위험 작업 전 확인 다이얼로그/체크리스트/주의 고지 추가

## 다음 단계 제안
1) 권한/테마/설치 집계 핫픽스 → 배포
2) 상태 판별 로직 업데이트 → 정확도 개선 릴리스
3) `NetWorks` 분리/사이드바 공통화 → 유지보수성 개선
4) MalwareBazaar 캐시/레이트 리밋 제어 → 안정화
5) 로깅/설정 옵션화 → 운영 편의성 향상

##
#주요 리스크/버그
관리자 권한 처리 UX: ProgramsList, NetWorks, Recoverys 생성자에서 관리자 아니면 Application.Current.Shutdown()으로 앱 종료. 
페이지 진입만으로 앱이 종료될 수 있어 UX가 급격함. 
권한 상승(재실행) 유도 또는 기능 제한으로 완화 필요.

BitLocker/보안 설정 강제 변경 위험: Recoverys에서 BitLocker 활성화/보호기 추가, Defender/Firewall 정책을 즉시 변경. 
사용자 동의/조건부 가드/롤백 안내 필요.

설치 프로그램 수집의 Win32_Product 사용: WmiHelper.GetInstalledPrograms()는 MSI 재구성 트리거+느림+권장되지 않음. 
현재 MainWindows에서 설치 프로그램 수 카운트에 사용됨. 레지스트리 방식으로 교체 필요.

보안 상태 정확도:
Defender: root\SecurityCenter2 AntiVirusProduct 존재 여부만 확인 → “활성” 오탐 가능.
Firewall: COM HNetCfg.FwMgr는 구형 API. 폴백은 있으나 정확성 개선 여지.
BitLocker: EncryptableVolume 존재 유무로 “활성/비활성” 판단 → 실제 보호 상태 아님.

테마 전환: Setting.ApplyTheme가 Application.Current.Resources.MergedDictionaries.Clear()로 전체 딕셔너리를 지워 MaterialDesign/기타 리소스를 날릴 수 있음. 테마 리소스만 교체하는 방식으로 수정 필요.

성능/응답성:
ProgramsList.ScanButton_Click 내 CollectInstalledPrograms() 실행 후 Thread.Sleep(5000) 인위적 지연 존재.
이벤트 로그 대량 조회/문자열 매핑이 UI 스레드에 부담 가능. 적절한 페이징/비동기 처리 필요.

코드 구조/유지보수성:
NetWorks.xaml.cs 2.7K+ 라인 단일 파일, 로직 과밀. 캡처/파서/뷰모델 분리 필요.
사이드바 XAML 반복. 공용 UserControl로 추출 권장.
ProgramInfo가 WmiHelper와 ProgramsList에 중복 정의. 네임 충돌/혼동 위험.
WindowsSentinel 네임스페이스 참조가 일부 파일에 남아 혼란 유발.

네트워크/외부 API:
MalwareBazaar 호출 시 레이트리밋 메시지 처리 존재하나, 설치 프로그램 전체 스캔은 호출량 큼. 배치/캐시 필요.
로깅: 동기 File.AppendAllText 반복 호출. 고빈도 시 I/O 병목 가능.
##
개선 제안(우선순위)
1.권한 처리 UX 개선
앱 시작 시 단일 권한 체크 → 필요 시 관리자 재실행 안내/자동 재실행.
페이지 생성자에서 앱 종료 제거, 기능 비활성/툴팁 안내로 대체.

2.설치 프로그램 집계 교체
MainWindows의 설치 프로그램 수 집계에서 WmiHelper.GetInstalledPrograms() 제거.
레지스트리 열람 기반 함수 재사용 또는 경량 카운트만 수행.

3.테마 전환 안정화
MergedDictionaries 전체 초기화 대신 테마 딕셔너리만 교체하고 MaterialDesign 리소스 유지.

4.보안 상태 판별 정확도 향상
Defender: Get-MpComputerStatus(PowerShell) 또는 root\Microsoft\Windows\Defender WMI 클래스 활용.
Firewall: NetSecurity 모듈(Get-NetFirewallProfile) 기반 확인로 통일.
BitLocker: Get-BitLockerVolume의 ProtectionStatus 확인.

5.구조 리팩터링
NetWorks를 캡처(SharpPcap), 파싱, 필터링/상태, UI(ViewModel)로 분리(MVVM).
사이드바 공용 UserControl화, 네비게이션 바인딩화.
ProgramInfo 단일 모델로 통합.

6.성능/UX
Thread.Sleep(5000) 제거, 진행률/개수 기준 실시간 업데이트.
로그 조회 페이징/시간 범위/검색 필터 기본 적용.

7.외부 API 사용 최적화
MalwareBazaar 해시 캐시(메모리/로컬 파일) 추가, 동시 호출 제한, 실패 재시도 백오프.

8.로깅 개선
비동기 로깅 또는 버퍼/큐 적용, 예외 포함 구조화 로그 옵션.

9.소규모 정정/클린업
using System.Windows.Data; 중복 제거(NetWorks.xaml.cs).
LogCheck.csproj의 UseWindowsForms 불필요 시 제거, AssemblyName 대소문자 통일.
미사용 Microsoft.Data.Sqlite 등 패키지 정리.
위험 작업(보안 설정 변경/BitLocker) 전 확인 다이얼로그와 롤백/가이드 링크 제공.
