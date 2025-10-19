# 🎯 WindowsSentinel 데모용 UDP Flood 생성 스크립트
# 관리자 권한 필요!

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  WindowsSentinel UDP Flood 생성기" -ForegroundColor Cyan
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

# 설정
$targetIP = "127.0.0.1"
$targetPorts = @(53, 123, 161, 500, 1900, 5060)  # DNS, NTP, SNMP, IKE, SSDP, SIP
$attackDuration = 30  # 초
$packetsPerSecond = 200
$payloadSize = 512  # bytes

Write-Host "📋 공격 설정:" -ForegroundColor Yellow
Write-Host "   대상 IP: $targetIP"
Write-Host "   대상 포트: $($targetPorts -join ', ')"
Write-Host "   지속 시간: $attackDuration 초"
Write-Host "   강도: $packetsPerSecond packets/sec"
Write-Host "   페이로드 크기: $payloadSize bytes"
Write-Host ""

Write-Host "⚠️ WindowsSentinel의 '데모 모드'를 활성화했는지 확인하세요!" -ForegroundColor Red
Write-Host ""
Write-Host "3초 후 시작합니다..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Write-Host ""
Write-Host "🚀 UDP Flood 시작!" -ForegroundColor Green
Write-Host ""

$startTime = Get-Date
$packetCount = 0
$bytesSent = 0

# UDP 클라이언트 생성
$udpClients = @{}
foreach ($port in $targetPorts) {
    $udpClients[$port] = New-Object System.Net.Sockets.UdpClient
}

# 랜덤 페이로드 생성
$payload = New-Object byte[] $payloadSize
$random = New-Object System.Random
$random.NextBytes($payload)

try {
    for ($i = 0; $i -lt $attackDuration; $i++) {
        for ($j = 0; $j -lt $packetsPerSecond; $j++) {
            try {
                $port = $targetPorts | Get-Random
                $client = $udpClients[$port]
                
                # UDP 패킷 전송
                $sent = $client.Send($payload, $payloadSize, $targetIP, $port)
                $packetCount++
                $bytesSent += $sent
                
                # 진행상황 표시 (매 500 패킷마다)
                if ($packetCount % 500 -eq 0) {
                    $mbSent = [math]::Round($bytesSent / 1MB, 2)
                    Write-Host "📊 전송: $packetCount 패킷 | $mbSent MB" -ForegroundColor Cyan
                }
                
            } catch {
                # 오류 무시
            }
            
            # 패킷 간격 조절
            Start-Sleep -Milliseconds (1000 / $packetsPerSecond)
        }
        
        # 1초 단위 진행 표시
        $elapsed = (Get-Date) - $startTime
        $pps = [math]::Round($packetCount / $elapsed.TotalSeconds, 0)
        Write-Host "⏱️  시간: $([int]$elapsed.TotalSeconds)/$attackDuration 초 | 속도: $pps pps" -ForegroundColor Yellow
    }
}
finally {
    # UDP 클라이언트 정리
    foreach ($client in $udpClients.Values) {
        $client.Close()
    }
}

Write-Host ""
Write-Host "✅ UDP Flood 완료!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 최종 통계:" -ForegroundColor Cyan
Write-Host "   총 패킷: $packetCount"
Write-Host "   총 전송량: $([math]::Round($bytesSent / 1MB, 2)) MB"
Write-Host "   실행 시간: $([int]((Get-Date) - $startTime).TotalSeconds) 초"
Write-Host "   평균 속도: $([int]($packetCount / ((Get-Date) - $startTime).TotalSeconds)) packets/sec"
Write-Host "   평균 대역폭: $([math]::Round(($bytesSent / ((Get-Date) - $startTime).TotalSeconds) / 1MB, 2)) MB/s"
Write-Host ""

Write-Host "💡 WindowsSentinel에서 다음을 확인하세요:" -ForegroundColor Yellow
Write-Host "   1. SecurityDashboard - UDP Flood 탐지"
Write-Host "   2. Toast 알림 - '보안 위협 탐지!' 메시지"
Write-Host "   3. AutoBlock - 127.0.0.1 차단 기록"
Write-Host "   4. Logs - UDP Flood 이벤트"
Write-Host ""

pause
