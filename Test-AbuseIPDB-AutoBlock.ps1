# AbuseIPDB AutoBlock 테스트 스크립트
# 실제 악성 IP로 연결을 시도하여 AutoBlock 기능을 테스트합니다.

param(
    [string]$ApiKey = "",  # AbuseIPDB API 키 (선택사항)
    [int]$MaxIPs = 3,     # 테스트할 최대 IP 개수
    [int]$DelaySeconds = 2 # 각 연결 시도 사이의 지연 시간
)

Write-Host "=== AbuseIPDB AutoBlock 테스트 스크립트 ===" -ForegroundColor Cyan
Write-Host "주의: 이 스크립트는 실제 악성 IP에 연결을 시도합니다!" -ForegroundColor Red
Write-Host ""
Write-Host "🔴 중요: LogCheck 애플리케이션에서 먼저 AbuseIPDB 테스트를 실행하세요!" -ForegroundColor Yellow
Write-Host "   → UI의 'AutoBlock 테스트' 버튼을 클릭하여 악성 IP 목록을 업데이트한 후" -ForegroundColor Yellow
Write-Host "   → 이 스크립트를 실행하면 실제 차단이 이루어집니다." -ForegroundColor Yellow
Write-Host ""

# 알려진 악성 IP 목록 (AbuseIPDB에서 확인된 IP들)
$KnownMaliciousIPs = @(
    "185.220.70.8",     # Tor exit node
    "45.95.169.157",    # Known malicious
    "198.98.60.19",     # Suspicious activity
    "89.248.165.2",     # Bot network
    "104.248.144.120"   # Malicious host
)

$TestPorts = @(80, 443, 22, 25, 8080, 3389, 1337)

function Test-IPConnection {
    param(
        [string]$IP,
        [int]$Port,
        [int]$TimeoutMs = 10000
    )
    
    try {
        Write-Host "  🔌 연결 시도: $IP`:$Port" -ForegroundColor Yellow
        
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $connectTask = $tcpClient.ConnectAsync($IP, $Port)
        
        # 타임아웃 처리
        $timeoutTask = [System.Threading.Tasks.Task]::Delay($TimeoutMs)
        $completedTask = [System.Threading.Tasks.Task]::WhenAny($connectTask, $timeoutTask).Result
        
        if ($completedTask -eq $connectTask -and $tcpClient.Connected) {
            Write-Host "    ✅ 연결 성공!" -ForegroundColor Green
            
            # 실제 데이터 송신으로 트래픽 생성
            try {
                $stream = $tcpClient.GetStream()
                $data = [System.Text.Encoding]::UTF8.GetBytes("GET / HTTP/1.1`r`nHost: test`r`nUser-Agent: PowerShell-AutoBlockTest/1.0`r`n`r`n")
                $stream.Write($data, 0, $data.Length)
                
                # 응답 읽기 시도
                $buffer = New-Object byte[] 1024
                $stream.ReadTimeout = 3000
                try {
                    $bytesRead = $stream.Read($buffer, 0, $buffer.Length)
                    if ($bytesRead -gt 0) {
                        Write-Host "    📦 응답 수신: $bytesRead bytes" -ForegroundColor Green
                    }
                }
                catch {
                    Write-Host "    ⚠️ 응답 읽기 실패: $($_.Exception.Message)" -ForegroundColor Yellow
                }
                
                # 연결 유지
                Start-Sleep -Seconds 3
                
            }
            catch {
                Write-Host "    ⚠️ 데이터 송수신 오류: $($_.Exception.Message)" -ForegroundColor Yellow
            }
            finally {
                $stream?.Close()
            }
            
            return "성공"
        }
        else {
            Write-Host "    ⏱️ 연결 타임아웃" -ForegroundColor Yellow
            return "타임아웃"
        }
        
    }
    catch {
        Write-Host "    ❌ 연결 실패: $($_.Exception.Message)" -ForegroundColor Red
        return "실패: $($_.Exception.Message)"
    }
    finally {
        $tcpClient?.Close()
    }
}

function Get-AbuseIPDBSuspiciousIPs {
    param(
        [string]$ApiKey,
        [int]$MaxResults = 5
    )
    
    if ([string]::IsNullOrEmpty($ApiKey)) {
        Write-Host "API 키가 없으므로 알려진 악성 IP를 사용합니다." -ForegroundColor Yellow
        return $KnownMaliciousIPs | Select-Object -First $MaxResults
    }
    
    try {
        Write-Host "AbuseIPDB API를 통해 의심스러운 IP를 조회 중..." -ForegroundColor Cyan
        
        $headers = @{
            "Key"    = $ApiKey
            "Accept" = "application/json"
        }
        
        $url = "https://api.abuseipdb.com/api/v2/blacklist?confidenceMinimum=75&limit=$MaxResults"
        $response = Invoke-RestMethod -Uri $url -Headers $headers -Method GET
        
        if ($response.data) {
            return $response.data | ForEach-Object { $_.ipAddress }
        }
        else {
            Write-Host "API 응답에서 IP를 찾을 수 없습니다. 알려진 악성 IP를 사용합니다." -ForegroundColor Yellow
            return $KnownMaliciousIPs | Select-Object -First $MaxResults
        }
        
    }
    catch {
        Write-Host "API 호출 실패: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "알려진 악성 IP를 사용합니다." -ForegroundColor Yellow
        return $KnownMaliciousIPs | Select-Object -First $MaxResults
    }
}

function Test-AbuseIPDBAutoBlock {
    Write-Host "🔍 의심스러운 IP 목록 조회 중..." -ForegroundColor Cyan
    $suspiciousIPs = Get-AbuseIPDBSuspiciousIPs -ApiKey $ApiKey -MaxResults $MaxIPs
    
    Write-Host "📋 테스트 대상 IP: $($suspiciousIPs -join ', ')" -ForegroundColor Cyan
    Write-Host ""
    
    $results = @()
    $totalTests = $suspiciousIPs.Count * 3  # IP당 3개 포트 테스트
    $currentTest = 0
    
    foreach ($ip in $suspiciousIPs) {
        Write-Host "--- $ip 테스트 시작 ---" -ForegroundColor Magenta
        
        # 각 IP에 대해 여러 포트 테스트
        $testPorts = $TestPorts | Get-Random -Count 3  # 랜덤하게 3개 포트 선택
        
        foreach ($port in $testPorts) {
            $currentTest++
            Write-Host "[$currentTest/$totalTests] " -NoNewline -ForegroundColor Gray
            
            $result = Test-IPConnection -IP $ip -Port $port
            $results += @{
                IP        = $ip
                Port      = $port
                Result    = $result
                Timestamp = Get-Date
            }
            
            Start-Sleep -Seconds $DelaySeconds
        }
        
        Write-Host "✅ $ip 테스트 완료" -ForegroundColor Green
        Write-Host ""
        
        # IP 간 추가 지연
        if ($ip -ne $suspiciousIPs[-1]) {
            Write-Host "다음 IP 테스트까지 3초 대기..." -ForegroundColor Gray
            Start-Sleep -Seconds 3
        }
    }
    
    return $results
}

function Show-TestResults {
    param($Results)
    
    Write-Host "=== 테스트 결과 요약 ===" -ForegroundColor Cyan
    Write-Host ""
    
    $groupedResults = $Results | Group-Object IP
    
    foreach ($group in $groupedResults) {
        $ip = $group.Name
        Write-Host "🎯 $ip" -ForegroundColor Yellow
        
        foreach ($test in $group.Group) {
            $status = switch ($test.Result) {
                { $_.StartsWith("성공") } { "✅" }
                { $_.StartsWith("타임아웃") } { "⏱️" }
                default { "❌" }
            }
            Write-Host "  $status Port $($test.Port): $($test.Result)" -ForegroundColor White
        }
        Write-Host ""
    }
    
    $successCount = ($Results | Where-Object { $_.Result.StartsWith("성공") }).Count
    $totalCount = $Results.Count
    
    Write-Host "📊 총 테스트: $totalCount, 성공: $successCount" -ForegroundColor Cyan
    
    if ($successCount -gt 0) {
        Write-Host "⚠️ 성공한 연결이 있습니다. LogCheck에서 AutoBlock이 작동했는지 확인하세요!" -ForegroundColor Yellow
    }
}

# 메인 실행
try {
    $confirmResult = Read-Host "실제 악성 IP에 연결을 시도합니다. 계속하시겠습니까? (Y/N)"
    if ($confirmResult -ne 'Y' -and $confirmResult -ne 'y') {
        Write-Host "테스트가 취소되었습니다." -ForegroundColor Yellow
        exit
    }
    
    Write-Host "🚀 AutoBlock 테스트 시작!" -ForegroundColor Green
    Write-Host "💡 LogCheck 애플리케이션에서 모니터링이 활성화되어 있는지 확인하세요!" -ForegroundColor Cyan
    Write-Host ""
    
    $testResults = Test-AbuseIPDBAutoBlock
    Show-TestResults -Results $testResults
    
    Write-Host ""
    Write-Host "🎉 테스트 완료!" -ForegroundColor Green
    Write-Host "📈 LogCheck 애플리케이션에서 AutoBlock 통계를 확인하세요." -ForegroundColor Cyan
    
}
catch {
    Write-Host "테스트 실행 중 오류가 발생했습니다: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "아무 키나 누르세요..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")