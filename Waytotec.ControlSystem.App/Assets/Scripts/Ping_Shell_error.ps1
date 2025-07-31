param (
    [string]$ip
)
while ($true) {
    $result = ping -n 1 -w 100 $ip
    $resultString = $result -join "`n"
    
    # 성공 응답 확인
    $successLine = $result | Where-Object {
        $_ -like "*time=*ms*" -or $_ -like "*시간=*ms*" -or $_ -like "*시간<1ms*" -or $_ -like "*time<1ms*"
    } | Select-Object -First 1
    
    # 실패 응답 확인
    $failedLine = $result | Where-Object {
        $_ -like "*시간이 만료되었습니다*" -or $_ -like "*timed out*" -or $_ -like "*Request timed out*" -or $_ -like "*요청 시간이 만료되었습니다*"
    } | Select-Object -First 1
    
    if ($successLine) {
        # 성공 시 응답 시간에 따라 색상 변경
        $time = $null
        if ($successLine -like "*time=*ms*") {
            $time = ($successLine -split "time=")[1] -split "ms"[0]
        } elseif ($successLine -like "*시간=*ms*") {
            $time = ($successLine -split "시간=")[1] -split "ms"[0]
        } elseif ($successLine -like "*시간<1ms*" -or $successLine -like "*time<1ms*") {
            $time = 0.5
        }
        
        $time = [double]$time
        if ($time -le 1) {
            Write-Host $successLine -ForegroundColor Green
        } elseif ($time -le 100) {
            Write-Host $successLine -ForegroundColor Yellow
        } else {
            Write-Host $successLine -ForegroundColor Red
        }
    } elseif ($failedLine) {
        # 실패 시 빨간색으로 표시
        Write-Host $ip $failedLine -ForegroundColor Red
    } else {
        # 기타 응답은 흰색으로 표시
        Write-Host $resultString -ForegroundColor White
    }
    
    Start-Sleep -Milliseconds 500
}