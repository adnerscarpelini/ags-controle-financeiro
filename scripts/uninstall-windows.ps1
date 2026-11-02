#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Remove o serviço AGS Finanças, a regra de firewall e os binários. A pasta de dados é preservada.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$serviceName = 'AgsFinance'
$firewallRuleName = 'AGS Finance'
$installDir = Join-Path $env:ProgramFiles 'AgsFinance'
$dataDir = Join-Path $env:ProgramData 'AgsFinance'

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Write-Host "Parando o serviço $serviceName..."
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    Write-Host "Removendo o serviço $serviceName..."
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc.exe falhou com código $LASTEXITCODE." }
}

Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule

if (Test-Path -LiteralPath $installDir) {
    Write-Host "Removendo $installDir..."
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

if ([System.Diagnostics.EventLog]::SourceExists($serviceName)) {
    [System.Diagnostics.EventLog]::DeleteEventSource($serviceName)
}

Write-Host "Desinstalado. Os dados foram preservados em $dataDir."
