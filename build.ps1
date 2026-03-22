$ErrorActionPreference = "Stop"

$racineProjet = Split-Path -Parent $MyInvocation.MyCommand.Path
$cheminProjet = Join-Path $racineProjet "RA.Compagnon\RA.Compagnon.csproj"
$dossierRelease = Join-Path $racineProjet "RA.Compagnon\bin\Release\net9.0-windows"
$dossierDebug = Join-Path $racineProjet "RA.Compagnon\bin\Debug\net9.0-windows"
$dossierSortie = Join-Path $racineProjet "dist\RA.Compagnon"

function Copier-BuildComplet {
    param (
        [string]$Source
    )

    if (Test-Path $dossierSortie) {
        Remove-Item $dossierSortie -Recurse -Force
    }

    New-Item -ItemType Directory -Path $dossierSortie -Force | Out-Null
    Copy-Item (Join-Path $Source "*") $dossierSortie -Recurse -Force
}

function Tenter-CompilationRelease {
    & dotnet build $cheminProjet -c Release --no-restore
    return $LASTEXITCODE -eq 0
}

# Évite les échecs de copie si l'application tourne encore.
$processus = Get-Process -Name "RA.Compagnon" -ErrorAction SilentlyContinue
if ($null -ne $processus) {
    $processus | Stop-Process -Force
}

$compilationReleaseReussie = Tenter-CompilationRelease

if ($compilationReleaseReussie -and (Test-Path $dossierRelease)) {
    Copier-BuildComplet -Source $dossierRelease
    Write-Host "Build complet Release copié dans : $dossierSortie"
    exit 0
}

if (Test-Path $dossierDebug) {
    Copier-BuildComplet -Source $dossierDebug
    Write-Warning "Compilation Release indisponible pour le moment. Dernière build locale copiée dans dist."
    Write-Host "Build complet copié dans : $dossierSortie"
    exit 0
}

throw "Aucune build exploitable n'est disponible pour alimenter le dossier dist."

