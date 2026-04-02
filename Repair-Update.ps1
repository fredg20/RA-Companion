[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$InstallDir,
    [string]$PackagePath,
    [switch]$NoRestart
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[RA-Compagnon] $Message"
}

function Resolve-InstallDir {
    param([string]$RequestedInstallDir)

    if (-not [string]::IsNullOrWhiteSpace($RequestedInstallDir)) {
        return (Resolve-Path -LiteralPath $RequestedInstallDir).Path
    }

    $currentDirectoryExecutable = Join-Path (Get-Location).Path "RA.Compagnon.exe"
    if (Test-Path -LiteralPath $currentDirectoryExecutable) {
        return (Get-Location).Path
    }

    $scriptDirectory = Split-Path -Parent $PSCommandPath
    $scriptDirectoryExecutable = Join-Path $scriptDirectory "RA.Compagnon.exe"
    if (Test-Path -LiteralPath $scriptDirectoryExecutable) {
        return $scriptDirectory
    }

    $processus = Get-Process RA.Compagnon -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.Path) } |
        Select-Object -First 1

    if ($null -ne $processus) {
        return Split-Path -Parent $processus.Path
    }

    throw "Impossible de déterminer le dossier d'installation. Lance le script depuis le dossier de Compagnon ou passe -InstallDir."
}

function Resolve-PackagePath {
    param(
        [string]$RequestedPackagePath,
        [string]$TargetVersion
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPackagePath)) {
        return (Resolve-Path -LiteralPath $RequestedPackagePath).Path
    }

    $updateDirectory = Join-Path $env:LOCALAPPDATA "RA-Compagnon\updates\$TargetVersion"
    $zipPath = Join-Path $updateDirectory "RA.Compagnon-win-x64.zip"
    $downloadPath = "$zipPath.download"

    if (Test-Path -LiteralPath $zipPath) {
        return $zipPath
    }

    if (Test-Path -LiteralPath $downloadPath) {
        Write-Step "Package temporaire trouvé, tentative de finalisation..."
        Move-Item -LiteralPath $downloadPath -Destination $zipPath -Force
        return $zipPath
    }

    throw "Aucun package local pour la version $TargetVersion n'a été trouvé dans $updateDirectory."
}

function Stop-RunningCompanion {
    param([string]$ResolvedInstallDir)

    $processus = Get-Process RA.Compagnon -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.Path) -and (Split-Path -Parent $_.Path) -eq $ResolvedInstallDir }

    foreach ($process in $processus) {
        Write-Step "Arrêt du processus RA.Compagnon ($($process.Id))..."
        Stop-Process -Id $process.Id -Force
    }

    if ($processus.Count -gt 0) {
        Start-Sleep -Milliseconds 600
    }
}

function Expand-Package {
    param([string]$ResolvedPackagePath)

    $extractDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("RA.Compagnon-bridge-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $extractDirectory -Force | Out-Null
    Expand-Archive -LiteralPath $ResolvedPackagePath -DestinationPath $extractDirectory -Force

    $rootItems = Get-ChildItem -LiteralPath $extractDirectory -Force
    if ($rootItems.Count -eq 1 -and $rootItems[0].PSIsContainer) {
        return $rootItems[0].FullName
    }

    return $extractDirectory
}

$resolvedInstallDir = Resolve-InstallDir -RequestedInstallDir $InstallDir
$resolvedPackagePath = Resolve-PackagePath -RequestedPackagePath $PackagePath -TargetVersion $Version

if (-not (Test-Path -LiteralPath (Join-Path $resolvedInstallDir "RA.Compagnon.exe"))) {
    throw "RA.Compagnon.exe est introuvable dans $resolvedInstallDir."
}

Write-Step "Dossier d'installation : $resolvedInstallDir"
Write-Step "Package utilisé : $resolvedPackagePath"

Stop-RunningCompanion -ResolvedInstallDir $resolvedInstallDir

$sourceRoot = Expand-Package -ResolvedPackagePath $resolvedPackagePath
Write-Step "Copie des fichiers..."

foreach ($item in (Get-ChildItem -LiteralPath $sourceRoot -Force)) {
    $destination = Join-Path $resolvedInstallDir $item.Name
    Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force
}

if (-not $NoRestart) {
    $exePath = Join-Path $resolvedInstallDir "RA.Compagnon.exe"
    Write-Step "Relance de Compagnon..."
    Start-Process -FilePath $exePath -WorkingDirectory $resolvedInstallDir | Out-Null
}

Write-Step "Réparation vers la version $Version terminée."
