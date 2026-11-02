#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala o AGS Finanças como Windows Service.

.DESCRIPTION
    Copia a publicação para %ProgramFiles%\AgsFinance, registra o serviço AgsFinance com início automático,
    cria a regra de entrada no Firewall do Windows para a porta e chama o comando "user create" quando ainda
    não existe banco. Pode ser executado de novo para atualizar os binários ou trocar a porta.

.PARAMETER SourceDir
    Pasta da publicação self-contained (dotnet publish -r win-x64). Padrão: artifacts\win-x64 na raiz do repositório.

.PARAMETER Port
    Porta HTTPS da aplicação. Padrão: 5280.

.PARAMETER FirewallProfile
    Perfis de rede em que a regra vale. Padrão: Private,Domain (não libera redes públicas).
#>
[CmdletBinding()]
param(
    [string]$SourceDir = (Join-Path $PSScriptRoot '..\artifacts\win-x64'),
    [ValidateRange(1, 65535)][int]$Port = 5280,
    [string]$FirewallProfile = 'Private,Domain'
)

$ErrorActionPreference = 'Stop'

$serviceName = 'AgsFinance'
$displayName = 'AGS Finanças'
$firewallRuleName = 'AGS Finance'
$installDir = Join-Path $env:ProgramFiles 'AgsFinance'
$dataDir = Join-Path $env:ProgramData 'AgsFinance'
$exeName = 'Ags.Finance.Web.exe'

$resolvedSource = Resolve-Path -LiteralPath $SourceDir -ErrorAction SilentlyContinue
if (-not $resolvedSource -or -not (Test-Path -LiteralPath (Join-Path $resolvedSource.Path $exeName))) {
    throw "Não encontrei $exeName em '$SourceDir'. Publique antes: dotnet publish src/Ags.Finance.Web -c Release -r win-x64 --self-contained -o artifacts/win-x64"
}
$SourceDir = $resolvedSource.Path

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Write-Host "Parando o serviço $serviceName..."
    Stop-Service -Name $serviceName -Force
    $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Write-Host "Copiando os binários para $installDir..."
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $installDir -Recurse -Force

# O Event Log exige a fonte registrada por um administrador; o serviço não tem permissão para criá-la.
if (-not [System.Diagnostics.EventLog]::SourceExists($serviceName)) {
    [System.Diagnostics.EventLog]::CreateEventSource($serviceName, 'Application')
}

# Conta virtual NT SERVICE\AgsFinance: sem senha e sem privilégios além dos que recebe nas pastas abaixo.
$binPath = '"' + (Join-Path $installDir $exeName) + '"'
$account = "NT SERVICE\$serviceName"
if (-not $existing) {
    Write-Host "Registrando o serviço $serviceName..."
    & sc.exe create $serviceName binPath= $binPath start= auto obj= $account DisplayName= $displayName | Out-Null
} else {
    & sc.exe config $serviceName binPath= $binPath start= auto obj= $account DisplayName= $displayName | Out-Null
}
if ($LASTEXITCODE -ne 0) { throw "sc.exe falhou com código $LASTEXITCODE." }
& sc.exe description $serviceName 'Controle financeiro pessoal acessado pelo navegador na rede local.' | Out-Null
# Sem ação de reinício: se a porta estiver em uso o serviço falha uma vez, registra o erro e fica parado.
& sc.exe failure $serviceName reset= 0 actions= '""' | Out-Null

# A porta vai por variável de ambiente do serviço (Hosting__Port), lida pela configuração da aplicação.
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name Environment `
    -Value @("Hosting__Port=$Port") -Type MultiString

# Pasta de dados fora de qualquer pasta sincronizada, só para administradores e para a conta do serviço.
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
& icacls.exe $dataDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'BUILTIN\Administrators:(OI)(CI)F' "${account}:(OI)(CI)M" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls falhou com código $LASTEXITCODE." }

Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port `
    -Profile $FirewallProfile -Description 'Acesso ao AGS Finanças pela rede local.' | Out-Null

# Cria o usuário único se o banco ainda não existe. O comando roda com a pasta de dados do serviço, senão o
# processo interativo do administrador usaria a pasta local dele. O comando chega no Lote 04; até lá a
# aplicação recusa o comando e o script apenas avisa.
$exePath = Join-Path $installDir $exeName
if (-not (Test-Path -LiteralPath (Join-Path $dataDir 'finance.db'))) {
    Write-Host 'Criando o usuário da aplicação...'
    $env:DataDirectory = $dataDir
    try {
        & $exePath user create
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "O comando 'user create' não foi concluído (código $LASTEXITCODE). Rode-o depois: `$env:DataDirectory='$dataDir'; & '$exePath' user create"
        }
    } finally {
        Remove-Item Env:\DataDirectory -ErrorAction SilentlyContinue
    }
}

Write-Host "Iniciando o serviço $serviceName..."
Start-Service -Name $serviceName

Write-Host ''
Write-Host 'AGS Finanças instalado. Acesse por um destes endereços:'
$addresses = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -ExpandProperty IPAddress -Unique
foreach ($address in $addresses) { Write-Host "  https://${address}:$Port" }
Write-Host "  https://$($env:COMPUTERNAME.ToLowerInvariant()):$Port"
Write-Host 'Na primeira visita o navegador avisa sobre o certificado; veja Configurações para confiar nele.'
if ($FirewallProfile -notmatch 'Public|Any') {
    Write-Host 'Se o celular não conectar, confira se a rede do Wi-Fi está como "Privada" ou reinstale com -FirewallProfile Any.'
}
