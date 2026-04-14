using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Local;

/*
 * Surveille les journaux et caches locaux des émulateurs afin de détecter
 * rapidement l'apparition de nouveaux signaux de succès.
 */
namespace RA.Compagnon.Services;

/*
 * Gère les FileSystemWatcher nécessaires à la détection locale des succès
 * selon la stratégie propre à chaque émulateur pris en charge.
 */
public sealed partial class ServiceSurveillanceSuccesLocaux : IDisposable
{
    private static readonly string CheminJournalSurveillanceSucces = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-surveillance-succes-locaux.log"
    );
    private static readonly TimeSpan DureeDebounceSignal = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan DelaiSignalInitialRetroArch = TimeSpan.FromMilliseconds(850);
    private static readonly TimeSpan DelaiSuiviRACacheLog = TimeSpan.FromMilliseconds(2500);
    private static readonly Regex RegexFichierJeuRACache = MyRegex();
    private readonly Lock _verrou = new();
    private FileSystemWatcher? _surveillancePrincipale;
    private FileSystemWatcher? _surveillanceSecondaire;
    private string _signatureCible = string.Empty;
    private DateTimeOffset _dernierSignalUtc = DateTimeOffset.MinValue;
    private bool _suiviRACacheLogPlanifie;

    public event Action<SignalSuccesLocal>? SignalRecu;

    /*
     * Réinitialise le journal de session dédié à la surveillance locale.
     */
    public static void ReinitialiserJournalSession()
    {
        _ = ServiceModeDiagnostic.ReinitialiserJournalSession(CheminJournalSurveillanceSucces);
    }

    /*
     * Journalise un événement de surveillance pour faciliter le diagnostic.
     */
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

    /*
     * Met à jour la cible surveillée à partir de l'état courant de la sonde
     * locale et reconstruit les watchers si nécessaire.
     */
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

    /*
     * Arrête proprement toutes les surveillances actuellement actives.
     */
    public void ArreterSurveillance()
    {
        lock (_verrou)
        {
            LibererSurveillances();
            _signatureCible = string.Empty;
            _suiviRACacheLogPlanifie = false;
        }
    }

    /*
     * Libère les ressources de surveillance détenues par l'instance.
     */
    public void Dispose()
    {
        ArreterSurveillance();
    }

    /*
     * Construit la paire de surveillances adaptée à la stratégie déclarée
     * pour l'émulateur ciblé.
     */
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
            StrategieSurveillanceSuccesLocale.JournalLogsSimple =>
                ConstruireSurveillanceJournalSimple(nomEmulateur),
            StrategieSurveillanceSuccesLocale.Project64RACache => ConstruireSurveillanceRACache(
                nomEmulateur,
                ServiceSourcesLocalesEmulateurs.TrouverRepertoireRACacheProject64(nomEmulateur)
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

    /*
     * Construit les watchers nécessaires pour la stratégie de journaux RetroArch.
     */
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

    /*
     * Construit une surveillance basée sur un journal unique pour les
     * émulateurs qui écrivent un fichier dédié.
     */
    private (
        FileSystemWatcher? Principale,
        FileSystemWatcher? Secondaire,
        string Signature
    ) ConstruireSurveillanceJournalSimple(string nomEmulateur)
    {
        string cheminJournal = ServiceSourcesLocalesEmulateurs.TrouverCheminJournalSuccesLocal(
            nomEmulateur
        );

        if (string.IsNullOrWhiteSpace(cheminJournal))
        {
            return (null, null, string.Empty);
        }

        string? repertoire = Path.GetDirectoryName(cheminJournal);
        string filtre = Path.GetFileName(cheminJournal);

        if (string.IsNullOrWhiteSpace(repertoire) || string.IsNullOrWhiteSpace(filtre))
        {
            return (null, null, string.Empty);
        }

        if (!Directory.Exists(repertoire))
        {
            return (null, null, string.Empty);
        }

        FileSystemWatcher principale = CreerSurveillance(nomEmulateur, "logs", repertoire, filtre);
        return (principale, null, $"{nomEmulateur}|{cheminJournal}");
    }

    /*
     * Construit les surveillances nécessaires aux émulateurs qui exposent
     * leurs signaux via RACache et RALog.
     */
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

    /*
     * Crée un FileSystemWatcher configuré pour notifier les changements utiles
     * sur un répertoire surveillé.
     */
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

        void gestionnaireEvenement(object _, FileSystemEventArgs evenement) =>
            SignalerChangement(nomEmulateur, typeSource, evenement.FullPath);
        RenamedEventHandler gestionnaireRenommage = (_, evenement) =>
            SignalerChangement(nomEmulateur, typeSource, evenement.FullPath);

        surveillance.Changed += gestionnaireEvenement;
        surveillance.Created += gestionnaireEvenement;
        surveillance.Renamed += gestionnaireRenommage;
        return surveillance;
    }

    /*
     * Réagit à un changement de fichier et émet, si nécessaire, un signal
     * local vers le reste de l'application.
     */
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

    /*
     * Libère les watchers actuellement enregistrés sans modifier la signature
     * de cible à elle seule.
     */
    private void LibererSurveillances()
    {
        _surveillancePrincipale?.Dispose();
        _surveillanceSecondaire?.Dispose();
        _surveillancePrincipale = null;
        _surveillanceSecondaire = null;
    }

    /*
     * Planifie un signal différé supplémentaire pour les stratégies RACache
     * qui nécessitent une seconde lecture du journal.
     */
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

    /*
     * Planifie le signal initial spécifique à RetroArch après un court délai
     * pour laisser le journal se stabiliser.
     */
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
            catch { }
        });
    }

    /*
     * Construit l'objet signal transmis au reste de l'application lorsqu'un
     * changement local pertinent est observé.
     */
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

    /*
     * Nettoie une chaîne avant de l'inscrire dans les journaux de diagnostic.
     */
    private static string Nettoyer(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /*
     * Détermine si un chemin modifié doit réellement déclencher un signal
     * pour la source surveillée.
     */
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
            "logs" => !string.IsNullOrWhiteSpace(nomFichier),
            _ => true,
        };
    }

    /*
     * Déclare l'expression régulière servant à reconnaître les fichiers jeu
     * issus de RACache.
     */
    [GeneratedRegex(@"^\d+\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
