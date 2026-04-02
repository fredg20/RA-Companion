using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Surveille les fichiers locaux RetroAchievements de certains émulateurs pour déclencher
/// des rafraîchissements plus réactifs que le simple tick API.
/// </summary>
public sealed class ServiceSurveillanceSuccesLocaux : IDisposable
{
    private static readonly string CheminJournalSurveillanceSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-surveillance-succes-locaux.log"
    );
    private static readonly TimeSpan DureeDebounceSignal = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan DelaiSignalInitialRetroArch = TimeSpan.FromMilliseconds(850);
    private static readonly TimeSpan DelaiSuiviRACacheLog = TimeSpan.FromMilliseconds(2500);
    private static readonly Regex RegexFichierJeuRACache = new(
        @"^\d+\.json$",
        RegexOptions.CultureInvariant
    );
    private readonly Lock _verrou = new();
    private FileSystemWatcher? _surveillancePrincipale;
    private FileSystemWatcher? _surveillanceSecondaire;
    private string _signatureCible = string.Empty;
    private DateTimeOffset _dernierSignalUtc = DateTimeOffset.MinValue;
    private bool _suiviRACacheLogPlanifie;

    public event Action<SignalSuccesLocal>? SignalRecu;

    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalSurveillanceSucces);
    }

    public static void JournaliserEvenement(string evenement, string details)
    {
        _ = ServiceModeDiagnostic.JournaliserLigne(
            CheminJournalSurveillanceSucces,
            string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={Nettoyer(evenement)};details={Nettoyer(details)}{Environment.NewLine}"
            )
        );
    }

    public void MettreAJourCible(EtatSondeLocaleEmulateur? etat)
    {
        string nomEmulateur = etat is { EmulateurDetecte: true }
            ? etat.NomEmulateur?.Trim() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            JournaliserEvenement("surveillance_arret", "raison=aucun_emulateur");
            ArreterSurveillance();
            return;
        }

        (FileSystemWatcher? principale, FileSystemWatcher? secondaire, string signature) =
            ConstruireSurveillances(nomEmulateur);

        lock (_verrou)
        {
            if (string.Equals(_signatureCible, signature, StringComparison.Ordinal))
            {
                principale?.Dispose();
                secondaire?.Dispose();
                return;
            }

            LibererSurveillances();
            _surveillancePrincipale = principale;
            _surveillanceSecondaire = secondaire;
            _signatureCible = signature;
        }

        JournaliserEvenement(
            "surveillance_cible",
            $"emulateur={nomEmulateur};signature={signature}"
        );

        if (ServiceCatalogueEmulateursLocaux.NecessiteSignalInitialSurveillance(nomEmulateur))
        {
            PlanifierSignalInitialRetroArch(signature);
        }
    }

    public void ArreterSurveillance()
    {
        lock (_verrou)
        {
            LibererSurveillances();
            _signatureCible = string.Empty;
            _suiviRACacheLogPlanifie = false;
        }
    }

    public void Dispose()
    {
        ArreterSurveillance();
    }

    private (
        FileSystemWatcher? Principale,
        FileSystemWatcher? Secondaire,
        string Signature
    ) ConstruireSurveillances(string nomEmulateur)
    {
        return ServiceCatalogueEmulateursLocaux
            .TrouverParNom(nomEmulateur)
            ?.StrategieSurveillanceSucces switch
        {
            StrategieSurveillanceSuccesLocale.RetroArchLogs => ConstruireSurveillanceRetroArch(),
            StrategieSurveillanceSuccesLocale.Project64RACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheProject64()
            ),
            StrategieSurveillanceSuccesLocale.RALibretroRACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRALibretro()
            ),
            StrategieSurveillanceSuccesLocale.RANesRACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRANes()
            ),
            StrategieSurveillanceSuccesLocale.RAVBARACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRAVBA()
            ),
            StrategieSurveillanceSuccesLocale.RASnes9xRACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheRASnes9x()
            ),
            _ => (null, null, string.Empty),
        };
    }

    private (
        FileSystemWatcher? Principale,
        FileSystemWatcher? Secondaire,
        string Signature
    ) ConstruireSurveillanceRetroArch()
    {
        string repertoireLogs = ServiceSourcesLocalesEmulateurs.TrouverRepertoireLogsRetroArch();

        if (string.IsNullOrWhiteSpace(repertoireLogs))
        {
            return (null, null, string.Empty);
        }

        FileSystemWatcher principale = CreerSurveillance(
            "RetroArch",
            "logs",
            repertoireLogs,
            "retroarch__*.log"
        );
        FileSystemWatcher secondaire = CreerSurveillance(
            "RetroArch",
            "logs",
            repertoireLogs,
            "retroarch.log"
        );
        return (principale, secondaire, $"RetroArch|{repertoireLogs}");
    }

    private (
        FileSystemWatcher? Principale,
        FileSystemWatcher? Secondaire,
        string Signature
    ) ConstruireSurveillanceRACache(string nomEmulateur, string repertoireRACache)
    {
        if (string.IsNullOrWhiteSpace(repertoireRACache))
        {
            return (null, null, string.Empty);
        }

        string repertoireData = Path.Combine(repertoireRACache, "Data");
        FileSystemWatcher? principale = Directory.Exists(repertoireData)
            ? CreerSurveillance(nomEmulateur, "racache_data", repertoireData, "*.json")
            : null;
        FileSystemWatcher? secondaire = Directory.Exists(repertoireRACache)
            ? CreerSurveillance(nomEmulateur, "racache_log", repertoireRACache, "RALog.txt")
            : null;

        return (principale, secondaire, $"{nomEmulateur}|{repertoireData}|{repertoireRACache}");
    }

    private FileSystemWatcher CreerSurveillance(
        string nomEmulateur,
        string typeSource,
        string repertoire,
        string filtre
    )
    {
        FileSystemWatcher surveillance = new(repertoire, filtre)
        {
            NotifyFilter =
                NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        FileSystemEventHandler gestionnaireEvenement = (_, evenement) =>
            SignalerChangement(nomEmulateur, typeSource, evenement.FullPath);
        RenamedEventHandler gestionnaireRenommage = (_, evenement) =>
            SignalerChangement(nomEmulateur, typeSource, evenement.FullPath);

        surveillance.Changed += gestionnaireEvenement;
        surveillance.Created += gestionnaireEvenement;
        surveillance.Renamed += gestionnaireRenommage;
        return surveillance;
    }

    private void SignalerChangement(string nomEmulateur, string typeSource, string chemin)
    {
        if (!CheminDoitDeclencherSignal(typeSource, chemin))
        {
            JournaliserEvenement(
                "signal_ignore_fichier",
                $"emulateur={nomEmulateur};source={typeSource};chemin={chemin}"
            );
            return;
        }

        lock (_verrou)
        {
            if (DateTimeOffset.UtcNow - _dernierSignalUtc < DureeDebounceSignal)
            {
                JournaliserEvenement(
                    "signal_ignore_debounce",
                    $"emulateur={nomEmulateur};source={typeSource};chemin={chemin}"
                );
                return;
            }

            _dernierSignalUtc = DateTimeOffset.UtcNow;
        }

        SignalRecu?.Invoke(ConstruireSignal(nomEmulateur, typeSource, chemin));

        JournaliserEvenement(
            "signal_recu",
            $"emulateur={nomEmulateur};source={typeSource};chemin={chemin}"
        );

        if (ServiceCatalogueEmulateursLocaux.TypeSourceDoitPlanifierSuivi(nomEmulateur, typeSource))
        {
            PlanifierSignalSuiviRACache(nomEmulateur, typeSource, chemin);
        }
    }

    private void LibererSurveillances()
    {
        _surveillancePrincipale?.Dispose();
        _surveillanceSecondaire?.Dispose();
        _surveillancePrincipale = null;
        _surveillanceSecondaire = null;
    }

    private void PlanifierSignalSuiviRACache(string nomEmulateur, string typeSource, string chemin)
    {
        string signatureCapturee;

        lock (_verrou)
        {
            if (_suiviRACacheLogPlanifie)
            {
                JournaliserEvenement(
                    "signal_suivi_ignore",
                    $"raison=deja_planifie;emulateur={nomEmulateur};source={typeSource};chemin={chemin}"
                );
                return;
            }

            _suiviRACacheLogPlanifie = true;
            signatureCapturee = _signatureCible;
        }

        JournaliserEvenement(
            "signal_suivi_planifie",
            $"emulateur={nomEmulateur};source={typeSource};delaiMs={DelaiSuiviRACacheLog.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)};chemin={chemin}"
        );

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DelaiSuiviRACacheLog);

                lock (_verrou)
                {
                    _suiviRACacheLogPlanifie = false;

                    if (
                        !string.Equals(_signatureCible, signatureCapturee, StringComparison.Ordinal)
                    )
                    {
                        JournaliserEvenement(
                            "signal_suivi_abandonne",
                            $"raison=cible_changee;emulateur={nomEmulateur};source={typeSource};chemin={chemin}"
                        );
                        return;
                    }
                }

                SignalRecu?.Invoke(ConstruireSignal(nomEmulateur, $"{typeSource}_suivi", chemin));
                JournaliserEvenement(
                    "signal_suivi_recu",
                    $"emulateur={nomEmulateur};source={typeSource}_suivi;chemin={chemin}"
                );
            }
            catch
            {
                lock (_verrou)
                {
                    _suiviRACacheLogPlanifie = false;
                }
            }
        });
    }

    private void PlanifierSignalInitialRetroArch(string signatureCapturee)
    {
        string repertoireLogs = ServiceSourcesLocalesEmulateurs.TrouverRepertoireLogsRetroArch();

        if (string.IsNullOrWhiteSpace(repertoireLogs) || !Directory.Exists(repertoireLogs))
        {
            return;
        }

        JournaliserEvenement(
            "signal_initial_planifie",
            $"emulateur=RetroArch;delaiMs={DelaiSignalInitialRetroArch.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)};repertoire={repertoireLogs}"
        );

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DelaiSignalInitialRetroArch);

                lock (_verrou)
                {
                    if (
                        !string.Equals(_signatureCible, signatureCapturee, StringComparison.Ordinal)
                    )
                    {
                        JournaliserEvenement(
                            "signal_initial_abandonne",
                            "raison=cible_changee;emulateur=RetroArch"
                        );
                        return;
                    }
                }

                FileInfo? fichierLog = new DirectoryInfo(repertoireLogs)
                    .EnumerateFiles("retroarch__*.log", SearchOption.TopDirectoryOnly)
                    .Where(fichier => fichier.Length > 0)
                    .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                    .FirstOrDefault();

                fichierLog ??= new DirectoryInfo(repertoireLogs)
                    .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                    .Where(fichier => fichier.Length > 0)
                    .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (fichierLog is null)
                {
                    return;
                }

                SignalRecu?.Invoke(
                    ConstruireSignal("RetroArch", "logs_initial", fichierLog.FullName)
                );
                JournaliserEvenement(
                    "signal_initial_recu",
                    $"emulateur=RetroArch;source=logs_initial;chemin={fichierLog.FullName}"
                );
            }
            catch
            {
                // Surveillance auxiliaire.
            }
        });
    }

    private static SignalSuccesLocal ConstruireSignal(
        string nomEmulateur,
        string typeSource,
        string chemin
    )
    {
        return new SignalSuccesLocal
        {
            NomEmulateur = nomEmulateur,
            TypeSource = typeSource,
            Chemin = chemin,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static bool CheminDoitDeclencherSignal(string typeSource, string chemin)
    {
        string nomFichier = Path.GetFileName(chemin);

        return typeSource switch
        {
            "racache_data" => RegexFichierJeuRACache.IsMatch(nomFichier),
            "racache_log" => string.Equals(
                nomFichier,
                "RALog.txt",
                StringComparison.OrdinalIgnoreCase
            ),
            "logs" => Path.GetExtension(nomFichier)
                .Equals(".log", StringComparison.OrdinalIgnoreCase),
            _ => true,
        };
    }
}
