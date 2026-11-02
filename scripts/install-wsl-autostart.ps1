<#
.SYNOPSIS
    Registra no Windows a tarefa que sobe o WSL 2, o Docker Engine e o Compose da aplicação sem terminal aberto (#59).

.DESCRIPTION
    Cria a tarefa agendada "AgsFinance-WSL", que roda `wsl.exe` com a distribuição e o usuário Linux informados,
    sobe o Compose da pasta do projeto e mantém a sessão do WSL viva (sem isso o WSL desliga quando não há
    processo em execução e os containers param). A política de restart do Compose NÃO inicia o WSL sozinha.

    Pré-requisitos na distribuição: systemd habilitado (/etc/wsl.conf com [boot] systemd=true) e o Docker Engine
    instalado como serviço (docker.service habilitado), para o `docker compose up -d` encontrar o daemon.

    O gatilho padrão é o logon do usuário Windows. Com -RunWithoutLogon a tarefa roda na inicialização do
    Windows, antes do logon (conta do usuário sem senha armazenada; depende de o WSL funcionar nesse contexto,
    o que deve ser validado reiniciando o Windows — veja docs/web/docker-deployment.md).

.PARAMETER Distribution
    Nome da distribuição WSL (`wsl -l -v`), por exemplo Ubuntu-24.04.

.PARAMETER LinuxUser
    Usuário Linux dono da instalação (o mesmo que roda `docker compose`).

.PARAMETER ProjectPath
    Pasta do projeto DENTRO do Linux (onde está o compose.yaml), por exemplo /home/maria/ags-controle-financeiro.

.PARAMETER RunWithoutLogon
    Roda na inicialização do Windows, sem esperar o logon.

.PARAMETER Remove
    Remove a tarefa em vez de criá-la.

.EXAMPLE
    ./scripts/install-wsl-autostart.ps1 -Distribution Ubuntu-24.04 -LinuxUser maria -ProjectPath /home/maria/ags-controle-financeiro -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, ParameterSetName = 'Install')][string]$Distribution,
    [Parameter(Mandatory, ParameterSetName = 'Install')][string]$LinuxUser,
    [Parameter(Mandatory, ParameterSetName = 'Install')][string]$ProjectPath,
    [Parameter(ParameterSetName = 'Install')][switch]$RunWithoutLogon,
    [Parameter(Mandatory, ParameterSetName = 'Remove')][switch]$Remove
)

$ErrorActionPreference = 'Stop'
$taskName = 'AgsFinance-WSL'

if ($Remove) {
    if ($PSCmdlet.ShouldProcess($taskName, 'Remover tarefa agendada')) {
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        Write-Host "Tarefa '$taskName' removida."
    }
    return
}

if ($ProjectPath -notmatch '^/[A-Za-z0-9._/-]+$') {
    throw "ProjectPath deve ser um caminho absoluto Linux simples (letras, números, ponto, hífen, sublinhado e barra): '$ProjectPath'."
}
if ($Distribution -notmatch '^[A-Za-z0-9._-]+$' -or $LinuxUser -notmatch '^[a-z_][a-z0-9_-]*$') {
    throw 'Distribution e LinuxUser contêm caracteres inválidos.'
}

# O comando roda no Linux: sobe o Compose e depois fica dormindo para manter a instância do WSL viva.
$linuxCommand = "cd $ProjectPath && docker compose up -d && exec sleep infinity"
$arguments = "-d $Distribution -u $LinuxUser --exec /bin/bash -c `"$linuxCommand`""
$action = New-ScheduledTaskAction -Execute "$env:SystemRoot\System32\wsl.exe" -Argument $arguments

$trigger = if ($RunWithoutLogon) { New-ScheduledTaskTrigger -AtStartup } else { New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME" }
$logonType = if ($RunWithoutLogon) { 'S4U' } else { 'Interactive' }
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType $logonType -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -MultipleInstances IgnoreNew `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)

if ($PSCmdlet.ShouldProcess($taskName, "Registrar tarefa agendada ($($trigger.CimClass.CimClassName)) para a distribuição $Distribution")) {
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
        -Description 'Sobe o WSL 2 e o Compose do ags-controle-financeiro sem terminal aberto.' -Force | Out-Null
    Write-Host "Tarefa '$taskName' registrada. Reinicie o Windows e confira: wsl -l -v e docker compose ps."
}
