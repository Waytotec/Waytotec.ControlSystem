param (
    [string]$ip
)

while ($true) {
    $result = ping -n 1 -w 500 $ip
    $line = $result | Where-Object { $_ -match "시간=" -or $_ -match "시간<" -or $_ -match "time=" -or $_ -match "time<" }

    if ($line -match "시간=([\d\.]+)ms") {
        $time = [double]$matches[1]
    } elseif ($line -match "시간<1ms") {
        $time = 0.5
    }

    if ($time -le 1) {
        Write-Host $line -ForegroundColor Green
    } elseif ($time -le 10) {
        Write-Host $line -ForegroundColor Yellow
    } else {
        Write-Host $line -ForegroundColor Red
    }

    Start-Sleep -Milliseconds 500
}
