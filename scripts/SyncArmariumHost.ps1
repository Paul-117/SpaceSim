param([switch]$NoPush)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$interNetwork = [System.Net.Sockets.AddressFamily]::InterNetwork

$candidates = foreach ($adapter in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()) {
    if ($adapter.OperationalStatus -ne [System.Net.NetworkInformation.OperationalStatus]::Up -or
        $adapter.NetworkInterfaceType -eq [System.Net.NetworkInformation.NetworkInterfaceType]::Loopback) {
        continue
    }

    $properties = $adapter.GetIPProperties()
    $hasIpv4Gateway = $properties.GatewayAddresses | Where-Object { $_.Address.AddressFamily -eq $interNetwork }
    foreach ($address in $properties.UnicastAddresses) {
        if ($address.Address.AddressFamily -ne $interNetwork) { continue }
        $value = $address.Address.ToString()
        if ($value.StartsWith('127.') -or $value.StartsWith('169.254.')) { continue }
        [PSCustomObject]@{
            Address = $value
            HasGateway = $null -ne $hasIpv4Gateway
            Interface = $adapter.Name
        }
    }
}

$selected = $candidates | Sort-Object @{ Expression = 'HasGateway'; Descending = $true }, Interface | Select-Object -First 1
if ($null -eq $selected) { throw 'No active LAN IPv4 address was found.' }

$hostFile = Join-Path $projectRoot 'Armarium-Host.txt'
$current = if (Test-Path -LiteralPath $hostFile) { (Get-Content -LiteralPath $hostFile -Raw).Trim() } else { '' }
if ($current -eq $selected.Address) {
    Write-Host "Armarium host unchanged: $($selected.Address) ($($selected.Interface))."
    exit 0
}

Set-Content -LiteralPath $hostFile -Value $selected.Address -NoNewline -Encoding ascii
Write-Host "Armarium host updated: $($selected.Address) ($($selected.Interface))."
if ($NoPush) { exit 0 }

& git -C $projectRoot add -- Armarium-Host.txt
if ($LASTEXITCODE -ne 0) { throw 'Could not stage Armarium-Host.txt.' }
& git -C $projectRoot commit --only -m 'Update Armarium host address' -- Armarium-Host.txt
if ($LASTEXITCODE -ne 0) { throw 'Could not commit Armarium-Host.txt.' }
& git -C $projectRoot push
if ($LASTEXITCODE -ne 0) { throw 'Could not push the Armarium host update.' }
Write-Host 'Armarium host update pushed. Run git pull on the Linux laptop.'
