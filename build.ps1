[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Dist,
    [switch]$NoClean,
    [switch]$NoKill,
    [switch]$SkipZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding

$RacineProjet = [System.IO.Path]::GetFullPath($PSScriptRoot)
$PrefixeRacineProjet = $RacineProjet.TrimEnd("\") + "\"
$CheminProjet = Join-Path $RacineProjet "RA.Compagnon\RA.Compagnon.csproj"
$CheminBin = Join-Path $RacineProjet "RA.Compagnon\bin"
$CheminObj = Join-Path $RacineProjet "RA.Compagnon\obj"
$CheminDist = Join-Path $RacineProjet "dist"
$CheminPublication = Join-Path $CheminDist "RA.Compagnon-win-x64"

function Ecrire-Etape {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Verifier-CheminDansProjet {
    param([string]$Chemin)

    $cheminAbsolu = [System.IO.Path]::GetFullPath($Chemin)
    if (
        -not $cheminAbsolu.Equals($RacineProjet, [System.StringComparison]::OrdinalIgnoreCase) `
        -and -not $cheminAbsolu.StartsWith($PrefixeRacineProjet, [System.StringComparison]::OrdinalIgnoreCase)
    ) {
        throw "Chemin hors du projet refuse : $cheminAbsolu"
    }

    return $cheminAbsolu
}

function Supprimer-DossierProjet {
    param([string]$Chemin)

    $cheminAbsolu = Verifier-CheminDansProjet -Chemin $Chemin
    if (Test-Path -LiteralPath $cheminAbsolu) {
        Remove-Item -LiteralPath $cheminAbsolu -Recurse -Force
    }
}

function Invoke-Dotnet {
    param([string[]]$Arguments)

    Write-Host ("dotnet " + ($Arguments -join " ")) -ForegroundColor DarkGray
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "La commande dotnet a echoue avec le code $LASTEXITCODE."
    }
}

function Lire-VersionProjet {
    [xml]$projet = Get-Content -LiteralPath $CheminProjet
    return [string]$projet.Project.PropertyGroup.Version
}

function Tester-GenerationWpfDisponible {
    param([string]$ConfigurationActive)

    $dossierIntermediaire = Join-Path $CheminObj $ConfigurationActive
    $fichiersAttendus = @(
        "App.g.cs",
        "GeneratedInternalTypeHelper.g.cs",
        "MainWindow.g.cs",
        "Views\Modules\BibliothequeModuleView.g.cs",
        "Views\Modules\DiagnosticModuleView.g.cs",
        "Views\Modules\ParametresModuleView.g.cs"
    )

    foreach ($fichier in $fichiersAttendus) {
        if (-not (Test-Path -LiteralPath (Join-Path $dossierIntermediaire $fichier))) {
            return $false
        }
    }

    return $true
}

function Construire-DotnetBuildArguments {
    param([string]$ConfigurationActive)

    return @("build", $CheminProjet, "-c", $ConfigurationActive, "-m:1")
}

function Construire-DotnetPublishArguments {
    param([string]$ConfigurationActive)

    return @(
        "publish",
        $CheminProjet,
        "-c",
        $ConfigurationActive,
        "-r",
        "win-x64",
        "--self-contained",
        "false",
        "-o",
        $CheminPublication,
        "-m:1"
    )
}

function Invoke-CommandeProjetAvecRattrapageWpf {
    param(
        [string[]]$Arguments,
        [string]$ConfigurationActive,
        [string]$Libelle
    )

    try {
        Invoke-Dotnet -Arguments $Arguments
    } catch {
        if (-not $NoClean) {
            $generationWpfDisponible = Tester-GenerationWpfDisponible -ConfigurationActive $ConfigurationActive
            if ($generationWpfDisponible) {
                Ecrire-Etape "$Libelle : seconde tentative automatique apres generation WPF"
                Invoke-Dotnet -Arguments $Arguments
                return
            }
        }

        throw
    }
}

if (-not (Test-Path -LiteralPath $CheminProjet)) {
    throw "Projet introuvable : $CheminProjet"
}

if (-not $NoKill) {
    Ecrire-Etape "Fermeture de RA.Compagnon si l'application est ouverte"
    $processus = Get-Process -Name "RA.Compagnon" -ErrorAction SilentlyContinue
    if ($null -ne $processus) {
        $processus | Stop-Process -Force
    }
}

Ecrire-Etape "Arret du serveur de build .NET"
Invoke-Dotnet -Arguments @("build-server", "shutdown")

if (-not $NoClean) {
    Ecrire-Etape "Nettoyage du projet"
    Invoke-Dotnet -Arguments @("clean", $CheminProjet, "-c", $Configuration)

    Ecrire-Etape "Purge des dossiers bin et obj"
    Supprimer-DossierProjet -Chemin $CheminBin
    Supprimer-DossierProjet -Chemin $CheminObj
}

if ($Dist) {
    if ($Configuration -ne "Release") {
        Write-Host "Publication dist : configuration Release forcee." -ForegroundColor Yellow
        $Configuration = "Release"
    }

    Ecrire-Etape "Preparation du dossier dist"
    New-Item -ItemType Directory -Force -Path $CheminDist | Out-Null
    Supprimer-DossierProjet -Chemin $CheminPublication
    New-Item -ItemType Directory -Force -Path $CheminPublication | Out-Null

    Ecrire-Etape "Publication portable dans dist"
    Invoke-CommandeProjetAvecRattrapageWpf `
        -Arguments (Construire-DotnetPublishArguments -ConfigurationActive $Configuration) `
        -ConfigurationActive $Configuration `
        -Libelle "Publication"

    if (-not $SkipZip) {
        $version = Lire-VersionProjet
        $cheminArchive = Join-Path $CheminDist "RA.Compagnon-win-x64-$version.zip"
        if (Test-Path -LiteralPath $cheminArchive) {
            Remove-Item -LiteralPath $cheminArchive -Force
        }

        Ecrire-Etape "Creation de l'archive ZIP"
        Compress-Archive -Path (Join-Path $CheminPublication "*") -DestinationPath $cheminArchive -Force
    }
} else {
    Ecrire-Etape "Build du projet"
    Invoke-CommandeProjetAvecRattrapageWpf `
        -Arguments (Construire-DotnetBuildArguments -ConfigurationActive $Configuration) `
        -ConfigurationActive $Configuration `
        -Libelle "Build"
}

Write-Host ""
Write-Host "Build termine avec succes." -ForegroundColor Green
