[CmdletBinding()]
param(
    [string]$RepositoryOwner = "fredg20",
    [string]$RepositoryName = "RA-Companion",
    [string]$ReleaseTagPrefix = "v",
    [string]$Notes = "Version courante publiée de RA-Compagnon.",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$racineProjet = Split-Path -Parent $MyInvocation.MyCommand.Path
$cheminProjet = Join-Path $racineProjet "RA.Compagnon\RA.Compagnon.csproj"
$cheminManifeste = Join-Path $racineProjet "update.json"
$dossierDist = Join-Path $racineProjet "dist"

[xml]$projetXml = Get-Content $cheminProjet
$version = $projetXml.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Impossible de lire la version du projet dans $cheminProjet."
}

$nomArchiveVersionnee = "RA.Compagnon-win-x64-$version.zip"
$tagRelease = "$ReleaseTagPrefix$version"
$urlArchive = "https://github.com/$RepositoryOwner/$RepositoryName/releases/download/$tagRelease/$nomArchiveVersionnee"
$datePublication = Get-Date -Format "yyyy-MM-dd"

if (-not $SkipBuild) {
    & (Join-Path $racineProjet "build.ps1")

    if ($LASTEXITCODE -ne 0) {
        throw "Le build du livrable a échoué."
    }
}

$cheminArchiveVersionnee = Join-Path $dossierDist $nomArchiveVersionnee
if (-not (Test-Path $cheminArchiveVersionnee)) {
    throw "L'archive versionnée attendue est introuvable : $cheminArchiveVersionnee"
}

$manifeste = [ordered]@{
    version = $version
    url = $urlArchive
    notes = $Notes
    publishedAt = $datePublication
}

$manifeste | ConvertTo-Json | Set-Content -LiteralPath $cheminManifeste -Encoding UTF8

Write-Host "Manifeste mis à jour : $cheminManifeste"
Write-Host "Version        : $version"
Write-Host "Tag release    : $tagRelease"
Write-Host "Archive locale : $cheminArchiveVersionnee"
Write-Host "URL publiée    : $urlArchive"
Write-Host ""
Write-Host "Étapes GitHub à suivre :"
Write-Host "1. Créer ou mettre à jour la release '$tagRelease'."
Write-Host "2. Joindre l'asset '$nomArchiveVersionnee'."
Write-Host "3. Commit et push de update.json."
