using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

public sealed class ServiceMiseAJourApplication
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(4) };
    private static readonly HttpClient HttpClientTelechargement = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
    };

    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string UrlManifesteVersionApplication =
        "https://raw.githubusercontent.com/fredg20/RA-Companion/main/update.json";

    public static EtatMiseAJourApplication CreerEtatInitial()
    {
        return EtatMiseAJourApplication.CreerEtatInitial(ObtenirVersionLocale());
    }

    public static string ObtenirVersionLocale()
    {
        return FormaterVersion(Assembly.GetExecutingAssembly().GetName().Version);
    }

    public static async Task<EtatMiseAJourApplication> VerifierAsync(
        CancellationToken jetonAnnulation = default
    )
    {
        string versionLocale = ObtenirVersionLocale();

        if (string.IsNullOrWhiteSpace(UrlManifesteVersionApplication))
        {
            return new EtatMiseAJourApplication(
                versionLocale,
                null,
                null,
                null,
                null,
                StatutMiseAJourApplication.NonConfiguree,
                "La vérification des mises à jour n'est pas configurée."
            );
        }

        try
        {
            using HttpResponseMessage reponse = await HttpClient.GetAsync(
                UrlManifesteVersionApplication,
                jetonAnnulation
            );

            if (!reponse.IsSuccessStatusCode)
            {
                return new EtatMiseAJourApplication(
                    versionLocale,
                    null,
                    null,
                    null,
                    null,
                    StatutMiseAJourApplication.VerificationImpossible,
                    "Impossible de vérifier les mises à jour pour le moment."
                );
            }

            string json = await reponse.Content.ReadAsStringAsync(jetonAnnulation);
            VersionDistanteApplication? manifeste =
                JsonSerializer.Deserialize<VersionDistanteApplication>(json, OptionsJson);

            if (manifeste is null || string.IsNullOrWhiteSpace(manifeste.Version))
            {
                return new EtatMiseAJourApplication(
                    versionLocale,
                    null,
                    null,
                    null,
                    null,
                    StatutMiseAJourApplication.VerificationImpossible,
                    "Le manifeste de mise à jour est invalide."
                );
            }

            return ConstruireEtatDepuisManifeste(versionLocale, manifeste);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new EtatMiseAJourApplication(
                versionLocale,
                null,
                null,
                null,
                null,
                StatutMiseAJourApplication.VerificationImpossible,
                "Impossible de vérifier les mises à jour pour le moment."
            );
        }
    }

    public static async Task<ResultatTelechargementMiseAJourApplication> TelechargerPackageAsync(
        EtatMiseAJourApplication etat,
        CancellationToken jetonAnnulation = default
    )
    {
        if (!etat.PeutTelecharger)
        {
            return new ResultatTelechargementMiseAJourApplication(
                false,
                false,
                null,
                "Aucune mise à jour téléchargeable n'est disponible."
            );
        }

        string versionDistante = string.IsNullOrWhiteSpace(etat.VersionDistante)
            ? "inconnue"
            : etat.VersionDistante;
        string dossierTelechargement = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RA-Compagnon",
            "updates",
            versionDistante
        );

        try
        {
            Directory.CreateDirectory(dossierTelechargement);

            string nomFichier = ObtenirNomFichierPackage(etat);
            string cheminFichier = Path.Combine(dossierTelechargement, nomFichier);
            string cheminTemporaire = $"{cheminFichier}.download";

            if (File.Exists(cheminFichier) && new FileInfo(cheminFichier).Length > 0)
            {
                return new ResultatTelechargementMiseAJourApplication(
                    true,
                    true,
                    cheminFichier,
                    $"La version {versionDistante} est déjà téléchargée."
                );
            }

            if (string.IsNullOrWhiteSpace(etat.UrlTelechargement))
            {
                return new ResultatTelechargementMiseAJourApplication(
                    false,
                    false,
                    null,
                    "Aucun lien de téléchargement n'est disponible."
                );
            }

            if (File.Exists(cheminTemporaire))
            {
                File.Delete(cheminTemporaire);
            }

            using HttpResponseMessage reponse = await HttpClientTelechargement.GetAsync(
                etat.UrlTelechargement,
                HttpCompletionOption.ResponseHeadersRead,
                jetonAnnulation
            );

            if (!reponse.IsSuccessStatusCode)
            {
                return new ResultatTelechargementMiseAJourApplication(
                    false,
                    false,
                    null,
                    "Impossible de télécharger la mise à jour pour le moment."
                );
            }

            await using (
                Stream fluxEntree = await reponse.Content.ReadAsStreamAsync(jetonAnnulation)
            )
            await using (
                FileStream fluxSortie = new(
                    cheminTemporaire,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                )
            )
            {
                await fluxEntree.CopyToAsync(fluxSortie, jetonAnnulation);
                await fluxSortie.FlushAsync(jetonAnnulation);
            }

            if (File.Exists(cheminFichier))
            {
                File.Delete(cheminFichier);
            }

            File.Move(cheminTemporaire, cheminFichier);

            return new ResultatTelechargementMiseAJourApplication(
                true,
                false,
                cheminFichier,
                $"La version {versionDistante} a été téléchargée."
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new ResultatTelechargementMiseAJourApplication(
                false,
                false,
                null,
                "Le téléchargement de la mise à jour a échoué."
            );
        }
    }

    public static string? TrouverPackageTelechargeExistant(EtatMiseAJourApplication etat)
    {
        if (!etat.PeutTelecharger)
        {
            return null;
        }

        string versionDistante = string.IsNullOrWhiteSpace(etat.VersionDistante)
            ? "inconnue"
            : etat.VersionDistante;
        string dossierTelechargement = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RA-Compagnon",
            "updates",
            versionDistante
        );

        if (!Directory.Exists(dossierTelechargement))
        {
            return null;
        }

        string cheminFichier = Path.Combine(dossierTelechargement, ObtenirNomFichierPackage(etat));

        if (!File.Exists(cheminFichier) || new FileInfo(cheminFichier).Length <= 0)
        {
            return null;
        }

        return cheminFichier;
    }

    public static ResultatLancementInstallationMiseAJourApplication LancerInstallationPackage(
        string cheminPackageZip,
        string? versionDistante,
        int identifiantProcessusParent,
        string? cheminExecutable = null
    )
    {
        if (string.IsNullOrWhiteSpace(cheminPackageZip) || !File.Exists(cheminPackageZip))
        {
            return new ResultatLancementInstallationMiseAJourApplication(
                false,
                "Le package téléchargé est introuvable."
            );
        }

        string dossierInstallation = AppContext.BaseDirectory;
        string cheminExecutableFinal = string.IsNullOrWhiteSpace(cheminExecutable)
            ? Path.Combine(dossierInstallation, "RA.Compagnon.exe")
            : cheminExecutable;

        if (!File.Exists(cheminExecutableFinal))
        {
            return new ResultatLancementInstallationMiseAJourApplication(
                false,
                "L'exécutable principal de Compagnon est introuvable."
            );
        }

        try
        {
            string dossierInstallationNormalise = dossierInstallation.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            string dossierInstalleur = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RA-Compagnon",
                "updates",
                "installer"
            );

            Directory.CreateDirectory(dossierInstalleur);

            string cheminScript = Path.Combine(
                dossierInstalleur,
                "installer-mise-a-jour.ps1"
            );
            string cheminLanceur = Path.Combine(
                dossierInstalleur,
                "lancer-installer-mise-a-jour.cmd"
            );
            string cheminJournal = Path.Combine(
                dossierInstalleur,
                "installer-mise-a-jour.log"
            );
            string cheminJournalLanceur = Path.Combine(
                dossierInstalleur,
                "lancer-installer-mise-a-jour.log"
            );

            File.WriteAllText(
                cheminScript,
                ConstruireScriptInstallation(),
                new UTF8Encoding(false)
            );

            string versionCible = string.IsNullOrWhiteSpace(versionDistante)
                ? "disponible"
                : versionDistante;
            File.WriteAllText(
                cheminLanceur,
                ConstruireScriptLanceur(
                    cheminScript,
                    cheminPackageZip,
                    dossierInstallationNormalise,
                    cheminExecutableFinal,
                    identifiantProcessusParent,
                    versionCible,
                    cheminJournal,
                    cheminJournalLanceur
                ),
                Encoding.ASCII
            );

            ProcessStartInfo informationsDemarrage = new()
            {
                FileName = cheminLanceur,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = dossierInstallation,
            };

            Process? processus = Process.Start(informationsDemarrage);

            if (processus is null)
            {
                return new ResultatLancementInstallationMiseAJourApplication(
                    false,
                    "Impossible de lancer l'installateur de mise à jour."
                );
            }

            return new ResultatLancementInstallationMiseAJourApplication(
                true,
                $"L'installation de la version {versionCible} a été lancée."
            );
        }
        catch
        {
            return new ResultatLancementInstallationMiseAJourApplication(
                false,
                "Impossible de lancer l'installation de la mise à jour."
            );
        }
    }

    private static EtatMiseAJourApplication ConstruireEtatDepuisManifeste(
        string versionLocale,
        VersionDistanteApplication manifeste
    )
    {
        string versionDistante = NormaliserVersionPourAffichage(manifeste.Version);
        int comparaison = ComparerVersions(versionDistante, versionLocale);

        if (comparaison > 0)
        {
            return new EtatMiseAJourApplication(
                versionLocale,
                versionDistante,
                string.IsNullOrWhiteSpace(manifeste.Url) ? null : manifeste.Url,
                manifeste.Notes,
                manifeste.PublishedAt,
                StatutMiseAJourApplication.MiseAJourDisponible,
                $"Mise à jour disponible : {versionDistante}"
            );
        }

        return new EtatMiseAJourApplication(
            versionLocale,
            versionDistante,
            string.IsNullOrWhiteSpace(manifeste.Url) ? null : manifeste.Url,
            manifeste.Notes,
            manifeste.PublishedAt,
            StatutMiseAJourApplication.AJour,
            "Application à jour."
        );
    }

    public static int ComparerVersions(string versionGauche, string versionDroite)
    {
        Version gauche = ConvertirEnVersionComparable(versionGauche);
        Version droite = ConvertirEnVersionComparable(versionDroite);
        return gauche.CompareTo(droite);
    }

    private static string NormaliserVersionPourAffichage(string version)
    {
        return FormaterVersion(ConvertirEnVersionComparable(version));
    }

    private static Version ConvertirEnVersionComparable(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return new Version(0, 0, 0, 0);
        }

        string versionNettoyee = version.Trim();
        int indexSuffixe = versionNettoyee.IndexOfAny(['-', '+']);

        if (indexSuffixe >= 0)
        {
            versionNettoyee = versionNettoyee[..indexSuffixe];
        }

        string[] morceaux = versionNettoyee.Split(
            '.',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        int[] valeurs = [0, 0, 0, 0];
        int longueur = Math.Min(morceaux.Length, valeurs.Length);

        for (int index = 0; index < longueur; index++)
        {
            if (
                !int.TryParse(
                    morceaux[index],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int valeur
                )
            )
            {
                return new Version(0, 0, 0, 0);
            }

            valeurs[index] = Math.Max(0, valeur);
        }

        return new Version(valeurs[0], valeurs[1], valeurs[2], valeurs[3]);
    }

    private static string FormaterVersion(Version? version)
    {
        if (version is null)
        {
            return "0.0.0";
        }

        return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private static string ObtenirNomFichierPackage(EtatMiseAJourApplication etat)
    {
        if (!string.IsNullOrWhiteSpace(etat.UrlTelechargement))
        {
            try
            {
                Uri uri = new(etat.UrlTelechargement);
                string nomFichier = Path.GetFileName(uri.LocalPath);

                if (!string.IsNullOrWhiteSpace(nomFichier))
                {
                    return nomFichier;
                }
            }
            catch
            {
                // On retombe alors sur un nom de secours.
            }
        }

        string versionDistante = string.IsNullOrWhiteSpace(etat.VersionDistante)
            ? "inconnue"
            : etat.VersionDistante;
        return $"RA.Compagnon-win-x64-{versionDistante}.zip";
    }

    private static string ConstruireScriptInstallation()
    {
        return """
param(
    [Parameter(Mandatory = $true)]
    [string]$ZipPath,
    [Parameter(Mandatory = $true)]
    [string]$InstallDir,
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,
    [Parameter(Mandatory = $true)]
    [int]$ParentProcessId,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$LogPath
)

$ErrorActionPreference = "Stop"

function Ecrire-Journal {
    param([string]$Message)
    $horodatage = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -LiteralPath $LogPath -Value "$horodatage $Message" -Encoding UTF8
}

function Planifier-NettoyageUpdates {
    param(
        [string]$UpdatesRoot,
        [string]$LogPath
    )

    $cheminCmd = Join-Path ([System.IO.Path]::GetTempPath()) ("RA.Compagnon-cleanup-" + [Guid]::NewGuid().ToString("N") + ".cmd")
    $contenuCmd = @"
@echo off
ping 127.0.0.1 -n 6 > nul
rmdir /s /q "$UpdatesRoot"
del /f /q "%~f0"
"@

    Set-Content -LiteralPath $cheminCmd -Value $contenuCmd -Encoding ASCII
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$cheminCmd`"" -WindowStyle Hidden | Out-Null
    Ecrire-Journal "Nettoyage du dossier updates planifié : $UpdatesRoot"
}

try {
    $dossierJournal = Split-Path -Parent $LogPath
    if (-not [string]::IsNullOrWhiteSpace($dossierJournal)) {
        New-Item -ItemType Directory -Path $dossierJournal -Force | Out-Null
    }

    Ecrire-Journal "Installation de la version $Version demandée."

    if ($ParentProcessId -gt 0) {
        Ecrire-Journal "Attente de la fermeture du processus parent : $ParentProcessId"

        for ($index = 0; $index -lt 40; $index++) {
            if (-not (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue)) {
                Ecrire-Journal "Le processus parent est bien fermé."
                break
            }

            Start-Sleep -Milliseconds 250
        }
    }

    $dossierExtraction = Join-Path ([System.IO.Path]::GetTempPath()) ("RA.Compagnon-update-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $dossierExtraction -Force | Out-Null
    Ecrire-Journal "Extraction du package : $ZipPath"
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $dossierExtraction -Force

    $elementsRacine = Get-ChildItem -LiteralPath $dossierExtraction -Force

    if ($elementsRacine.Count -eq 1 -and $elementsRacine[0].PSIsContainer) {
        $sourceRoot = $elementsRacine[0].FullName
    }
    else {
        $sourceRoot = $dossierExtraction
    }

    foreach ($element in (Get-ChildItem -LiteralPath $sourceRoot -Force)) {
        $destination = Join-Path $InstallDir $element.Name
        Ecrire-Journal "Copie : $($element.FullName) -> $destination"
        Copy-Item -LiteralPath $element.FullName -Destination $destination -Recurse -Force
    }

    Ecrire-Journal "Relance de l'application : $ExecutablePath"
    $processusRelance = Start-Process -FilePath $ExecutablePath -WorkingDirectory $InstallDir -PassThru
    Ecrire-Journal "Application relancée avec le PID $($processusRelance.Id)."

    $updatesRoot = Split-Path -Parent $dossierJournal
    if (-not [string]::IsNullOrWhiteSpace($updatesRoot)) {
        Planifier-NettoyageUpdates -UpdatesRoot $updatesRoot -LogPath $LogPath
    }

    Ecrire-Journal "Installation terminée."
}
catch {
    Ecrire-Journal "Échec de l'installation : $($_.Exception.Message)"
}
""";
    }

    private static string ConstruireScriptLanceur(
        string cheminScript,
        string cheminPackageZip,
        string dossierInstallation,
        string cheminExecutable,
        int identifiantProcessusParent,
        string versionCible,
        string cheminJournal,
        string cheminJournalLanceur
    )
    {
        return
            "@echo off\r\n"
            + $"echo %date% %time% Lanceur démarré > \"{cheminJournalLanceur}\"\r\n"
            + "start \"\" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden"
            + $" -File \"{cheminScript}\""
            + $" -ZipPath \"{cheminPackageZip}\""
            + $" -InstallDir \"{dossierInstallation}\""
            + $" -ExecutablePath \"{cheminExecutable}\""
            + $" -ParentProcessId {identifiantProcessusParent}"
            + $" -Version \"{versionCible}\""
            + $" -LogPath \"{cheminJournal}\"\r\n"
            + $"echo %date% %time% Lanceur terminé >> \"{cheminJournalLanceur}\"\r\n";
    }
}
