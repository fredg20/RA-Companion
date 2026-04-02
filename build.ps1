$ErrorActionPreference = "Stop"

$racineProjet = Split-Path -Parent $MyInvocation.MyCommand.Path
$cheminProjet = Join-Path $racineProjet "RA.Compagnon\RA.Compagnon.csproj"
$runtimeIdentifier = "win-x64"
$nomLivrable = "RA.Compagnon-$runtimeIdentifier"
$dossierSortie = Join-Path $racineProjet "dist\$nomLivrable"
$dossierSortieTemporaire = Join-Path $racineProjet "dist\$nomLivrable.tmp"
$cheminArchiveGenerique = Join-Path $racineProjet "dist\$nomLivrable.zip"
$versionProjet = ([xml](Get-Content $cheminProjet)).Project.PropertyGroup.Version
$nomArchiveVersionnee = "$nomLivrable-$versionProjet.zip"
$cheminArchiveVersionnee = Join-Path $racineProjet "dist\$nomArchiveVersionnee"

# Evite les echecs si l'application tourne encore.
$processus = Get-Process -Name "RA.Compagnon" -ErrorAction SilentlyContinue
if ($null -ne $processus) {
    $processus | Stop-Process -Force
}

if (Test-Path $dossierSortieTemporaire) {
    Remove-Item $dossierSortieTemporaire -Recurse -Force
}

New-Item -ItemType Directory -Path $dossierSortieTemporaire -Force | Out-Null

& dotnet restore $cheminProjet -r $runtimeIdentifier

if ($LASTEXITCODE -ne 0) {
    throw "La restauration pour le runtime de publication a echoue."
}

& dotnet publish $cheminProjet `
    -c Release `
    -r $runtimeIdentifier `
    --self-contained true `
    -m:1 `
    /p:PublishSingleFile=false `
    /p:PublishReadyToRun=false `
    /p:PublishDir="$dossierSortieTemporaire\\"

if ($LASTEXITCODE -ne 0) {
    throw "La publication vers dist a echoue."
}

if (-not (Test-Path $dossierSortieTemporaire)) {
    throw "La publication a reussi mais le dossier dist attendu n'a pas ete genere."
}

if (Test-Path $dossierSortie) {
    Remove-Item $dossierSortie -Recurse -Force
}

if (Test-Path $cheminArchiveGenerique) {
    Remove-Item $cheminArchiveGenerique -Force
}

if (Test-Path $cheminArchiveVersionnee) {
    Remove-Item $cheminArchiveVersionnee -Force
}

if (Test-Path (Join-Path $racineProjet "dist\Repair-Update.ps1")) {
    Remove-Item (Join-Path $racineProjet "dist\Repair-Update.ps1") -Force
}

if (Test-Path (Join-Path $racineProjet "dist\Repair-Update-1.0.3-to-1.0.4.ps1")) {
    Remove-Item (Join-Path $racineProjet "dist\Repair-Update-1.0.3-to-1.0.4.ps1") -Force
}

Move-Item -LiteralPath $dossierSortieTemporaire -Destination $dossierSortie

Compress-Archive -Path (Join-Path $dossierSortie "*") -DestinationPath $cheminArchiveVersionnee

Write-Host "Livrable autonome genere dans : $dossierSortie"
Write-Host "Archive versionnee generee dans : $cheminArchiveVersionnee"
