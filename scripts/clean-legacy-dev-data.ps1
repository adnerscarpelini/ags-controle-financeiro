<#
.SYNOPSIS
    Remove os arquivos SQLite descartáveis de desenvolvimento deixados pela versão anterior (#60).

.DESCRIPTION
    Apaga somente finance.db, finance.db-wal e finance.db-shm da pasta de dados padrão do `dotnet run`
    (%LOCALAPPDATA%\AgsFinance no Windows, ~/.local/share/ags-finance no Linux) ou da pasta informada.
    Não toca em certificados nem em qualquer outro arquivo ou pasta, e não remove a pasta.
    Use -WhatIf para só listar os alvos. Dados de instalações PostgreSQL não passam por aqui.

.PARAMETER DataDirectory
    Pasta de dados a limpar. Sem valor, usa a pasta padrão do desenvolvimento.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$DataDirectory
)

$ErrorActionPreference = 'Stop'

if (-not $DataDirectory) {
    $localData = [Environment]::GetFolderPath('LocalApplicationData')
    $folder = if ($IsLinux -or $IsMacOS) { 'ags-finance' } else { 'AgsFinance' }
    $DataDirectory = Join-Path $localData $folder
}

if (-not (Test-Path -LiteralPath $DataDirectory -PathType Container)) {
    Write-Host "Nada a limpar: a pasta '$DataDirectory' não existe."
    return
}

$targets = 'finance.db', 'finance.db-wal', 'finance.db-shm' |
    ForEach-Object { Join-Path $DataDirectory $_ } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }

if (-not $targets) {
    Write-Host "Nada a limpar em '$DataDirectory'."
    return
}

foreach ($target in $targets) {
    if ($PSCmdlet.ShouldProcess($target, 'Remover arquivo SQLite descartável')) {
        Remove-Item -LiteralPath $target -Force
        Write-Host "Removido: $target"
    }
}
