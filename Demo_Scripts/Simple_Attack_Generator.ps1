# 🎯 WindowsSentinel 데모용 실제 트래픽 생성 스크립트
# 관리자 권한 필요!

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  WindowsSentinel 데모 트래픽 생성기" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 관리자 권한 확인
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "⚠️ 관리자 권한이 필요합니다!" -ForegroundColor Red
    Write-Host "PowerShell을 관리자 권한으로 다시 실행하세요." -ForegroundColor Yellow
    pause
    exit
}

Write-Host "✅ 관리자 권한 확인 완료" -ForegroundColor Green
Write-Host ""

# 테스트 포트 설정
$targetIP = "127.0.0.1"
$targetPorts = @(80, 443, 8080, 3389, 445, 135, 139)
$attackDuration = 30  # 초
$packetsPerSecond = 100

Write-Host "📋 공격 설정:" -ForegroundColor Yellow
Write-Host "   대상 IP: $targetIP"
Write-Host "   대상 포트: $($targetPorts -join ', ')"
Write-Host "   지속 시간: $attackDuration 초"
Write-Host "   강도: $packetsPerSecond packets/sec"
Write-Host ""

Write-Host "⚠️ WindowsSentinel의 '데모 모드'를 활성화했는지 확인하세요!" -ForegroundColor Red
Write-Host ""
Write-Host "3초 후 시작합니다..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "🚀 트래픽 생성 시작!" -ForegroundColor Green
Write-Host ""

$startTime = Get-Date
$packetCount = 0
$connectionErrors = 0

# SYN Flood 시뮬레이션 (TCP 연결 시도)
for ($i = 0; $i -lt $attackDuration; $i++) {
    $loopStart = Get-Date
    
    for ($j = 0; $j -lt $packetsPerSecond; $j++) {
        try {
            $port = $targetPorts | Get-Random
            $client = New-Object System.Net.Sockets.TcpClient
            
            # 타임아웃 설정 (매우 짧게)
            $client.ReceiveTimeout = 10
            $client.SendTimeout = 10
            
            # 비동기 연결 시도 (즉시 닫음 - SYN Flood 효과)
            $asyncResult = $client.BeginConnect($targetIP, $port, $null, $null)
            
            # 매우 짧은 대기 후 연결 종료
            Start-Sleep -Milliseconds 1
            
            try {
                $client.Close()
            } catch {}
            
            $packetCount++
            
            # 진행상황 표시 (매 100 패킷마다)
            if ($packetCount % 100 -eq 0) {
                Write-Host "📊 전송된 패킷: $packetCount | 오류: $connectionErrors" -ForegroundColor Cyan
            }
            
        } catch {
            $connectionErrors++
        }
        
        # 패킷 간격 조절 (초당 패킷 수 제어)
        Start-Sleep -Milliseconds (1000 / $packetsPerSecond)
    }
    
    # 1초 단위 진행 표시
    $elapsed = (Get-Date) - $startTime
    Write-Host "⏱️  진행 시간: $([int]$elapsed.TotalSeconds)/$attackDuration 초" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ 트래픽 생성 완료!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 최종 통계:" -ForegroundColor Cyan
Write-Host "   총 패킷: $packetCount"
Write-Host "   총 오류: $connectionErrors"
Write-Host "   실행 시간: $([int]((Get-Date) - $startTime).TotalSeconds) 초"
Write-Host "   평균 속도: $([int]($packetCount / ((Get-Date) - $startTime).TotalSeconds)) packets/sec"
Write-Host ""

Write-Host "💡 WindowsSentinel에서 다음을 확인하세요:" -ForegroundColor Yellow
Write-Host "   1. SecurityDashboard - 실시간 차트 업데이트"
Write-Host "   2. Toast 알림 - '보안 위협 탐지!' 메시지"
Write-Host "   3. AutoBlock - 127.0.0.1 차단 기록"
Write-Host "   4. Logs - 탐지 이벤트 로그"
Write-Host ""

pause
