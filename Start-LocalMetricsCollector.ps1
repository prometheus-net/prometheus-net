$ErrorActionPreference = "Stop"

# This starts the local metrics collector used at development-time to upload metrics via remote write.

function VerifySuccess {
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Last command failed. See log for details."
    }
}

$adapter = Get-NetIPAddress | ? InterfaceAlias -like "*(WSL)*" | ? AddressFamily -eq IPv4

if (!$adapter) {
    Write-Error "Unable to find WSL network adapter."
}

[string]$host_ip = $adapter.IPAddress

& docker build --tag prometheus-net-collector --file .\LocalMetricsCollector\Dockerfile (Join-Path $PSScriptRoot LocalMetricsCollector)
VerifySuccess

$apiKeyCredential = Get-Credential -Title "Enter Grafana Cloud API credentials"
$apiKey = ConvertFrom-SecureString -SecureString $apiKeyCredential.Password -AsPlainText

& docker run -it --rm --env PROMETHEUS_PORT=9090 --env GRAFANA_USER=$($apiKeyCredential.UserName) --env GRAFANA_API_KEY=$apiKey --env COMPUTERNAME=$env:COMPUTERNAME --env HOST_IP=$host_ip --publish 9090:9090 --name prometheus-net-collector prometheus-net-collector
VerifySuccess