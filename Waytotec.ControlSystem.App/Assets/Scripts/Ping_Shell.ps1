param (
    [string]$ip
)

while ($true) {
    $result = ping -n 1 -w 100 $ip
    $line = $result | Where-Object { $_ -match "time=" }

    if ($line -match "time=([\d\.]+)ms") {
        $time = [double]$matches[1]

        if ($time -le 1) {
            Write-Host $line -ForegroundColor Green
        } elseif ($time -le 10) {
            Write-Host $line -ForegroundColor Yellow
        } else {
            Write-Host $line -ForegroundColor Red
        }
    } else {
        Write-Host $result -ForegroundColor Magenta
    }

    Start-Sleep -Milliseconds 10
}
