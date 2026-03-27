$ErrorActionPreference = "Stop"

$racineProjet = Split-Path -Parent $MyInvocation.MyCommand.Path
$cheminSolution = Join-Path $racineProjet "RA.Compagnon.sln"
$dossierSortie = Join-Path $racineProjet "dist\RA.Compagnon"
$dossierSortieTemporaire = Join-Path $racineProjet "dist\RA.Compagnon.tmp"

# Évite les échecs si l'application tourne encore.
$processus = Get-Process -Name "RA.Compagnon" -ErrorAction SilentlyContinue
if ($null -ne $processus) {
    $processus | Stop-Process -Force
}

if (Test-Path $dossierSortieTemporaire) {
    Remove-Item $dossierSortieTemporaire -Recurse -Force
}

New-Item -ItemType Directory -Path $dossierSortieTemporaire -Force | Out-Null

& dotnet build $cheminSolution `
    -m:1 `
    --no-restore `
    /p:OutputPath="$dossierSortieTemporaire\\"

if ($LASTEXITCODE -ne 0) {
    throw "La build vers dist a echoue."
}

if (-not (Test-Path $dossierSortieTemporaire)) {
    throw "La build a reussi mais le dossier dist attendu n'a pas ete genere."
}

if (Test-Path $dossierSortie) {
    Remove-Item $dossierSortie -Recurse -Force
}

Move-Item -LiteralPath $dossierSortieTemporaire -Destination $dossierSortie

Write-Host "Build genere dans : $dossierSortie"
