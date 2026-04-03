using System.Diagnostics;
using System.IO;
using System.Text;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public static class ServiceSourcesLocalesEmulateurs
{
    private static readonly Lock VerrouEmplacementsEmulateursManuels = new();
    private static readonly Lock VerrouEmplacementsEmulateursDetectes = new();
    private static Dictionary<string, string> _emplacementsEmulateursManuels = [];
    private static Dictionary<string, string> _emplacementsEmulateursDetectes = [];

    public static void ConfigurerEmplacementsEmulateursManuels(
        IReadOnlyDictionary<string, string>? emplacements
    )
    {
        lock (VerrouEmplacementsEmulateursManuels)
        {
            _emplacementsEmulateursManuels = emplacements?
                .Where(entree =>
                    !string.IsNullOrWhiteSpace(entree.Key) && !string.IsNullOrWhiteSpace(entree.Value)
                )
                .ToDictionary(
                    entree => entree.Key.Trim(),
                    entree => entree.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase
                ) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void ConfigurerEmplacementsEmulateursDetectes(
        IReadOnlyDictionary<string, string>? emplacements
    )
    {
        lock (VerrouEmplacementsEmulateursDetectes)
        {
            _emplacementsEmulateursDetectes = emplacements?
                .Where(entree =>
                    !string.IsNullOrWhiteSpace(entree.Key)
                    && !string.IsNullOrWhiteSpace(entree.Value)
                    && CheminExecutableCorrespondEmulateur(entree.Key, entree.Value)
                )
                .ToDictionary(
                    entree => entree.Key.Trim(),
                    entree => entree.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase
                ) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static string ObtenirEmplacementEmulateurManuel(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return string.Empty;
        }

        lock (VerrouEmplacementsEmulateursManuels)
        {
            return _emplacementsEmulateursManuels.TryGetValue(nomEmulateur.Trim(), out string? chemin)
                ? chemin
                : string.Empty;
        }
    }

    public static string ObtenirEmplacementEmulateurDetecte(string nomEmulateur)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur))
        {
            return string.Empty;
        }

        lock (VerrouEmplacementsEmulateursDetectes)
        {
            return _emplacementsEmulateursDetectes.TryGetValue(
                nomEmulateur.Trim(),
                out string? chemin
            )
                ? chemin
                : string.Empty;
        }
    }

    public static bool MemoriserEmplacementEmulateurDetecte(
        string nomEmulateur,
        string cheminExecutable
    )
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur) || string.IsNullOrWhiteSpace(cheminExecutable))
        {
            return false;
        }

        string cheminNormalise;

        try
        {
            cheminNormalise = Path.GetFullPath(cheminExecutable.Trim());
        }
        catch
        {
            return false;
        }

        if (!File.Exists(cheminNormalise) && !Directory.Exists(cheminNormalise))
        {
            return false;
        }

        if (!CheminExecutableCorrespondEmulateur(nomEmulateur, cheminNormalise))
        {
            return false;
        }

        lock (VerrouEmplacementsEmulateursDetectes)
        {
            if (
                _emplacementsEmulateursDetectes.TryGetValue(
                    nomEmulateur.Trim(),
                    out string? cheminExistant
                )
                && string.Equals(cheminExistant, cheminNormalise, StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            _emplacementsEmulateursDetectes[nomEmulateur.Trim()] = cheminNormalise;
            return true;
        }
    }

    public static bool CorrespondAuCheminEmulateurManuel(string nomEmulateur, string cheminExecutable)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur) || string.IsNullOrWhiteSpace(cheminExecutable))
        {
            return false;
        }

        string cheminManuel = ObtenirEmplacementEmulateurManuel(nomEmulateur);

        if (string.IsNullOrWhiteSpace(cheminManuel))
        {
            return false;
        }

        try
        {
            string cheminExecutableNormalise = Path.GetFullPath(cheminExecutable.Trim());

            if (File.Exists(cheminManuel))
            {
                string cheminManuelNormalise = Path.GetFullPath(cheminManuel);
                return string.Equals(
                    cheminExecutableNormalise,
                    cheminManuelNormalise,
                    StringComparison.OrdinalIgnoreCase
                );
            }

            if (Directory.Exists(cheminManuel))
            {
                string? repertoireExecutable = Path.GetDirectoryName(cheminExecutableNormalise);

                if (string.IsNullOrWhiteSpace(repertoireExecutable))
                {
                    return false;
                }

                string repertoireManuelNormalise = Path.GetFullPath(cheminManuel);
                return string.Equals(
                        repertoireExecutable,
                        repertoireManuelNormalise,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || cheminExecutableNormalise.StartsWith(
                        repertoireManuelNormalise + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase
                    );
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static string TrouverEmplacementEmulateur(string nomEmulateur)
    {
        string emplacementManuel = ObtenirEmplacementEmulateurManuel(nomEmulateur);

        if (!string.IsNullOrWhiteSpace(emplacementManuel))
        {
            return emplacementManuel;
        }

        string emplacementDetecteMemoire = ObtenirEmplacementEmulateurDetecte(nomEmulateur);

        if (!string.IsNullOrWhiteSpace(emplacementDetecteMemoire))
        {
            return emplacementDetecteMemoire;
        }

        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null)
        {
            return string.Empty;
        }

        string emplacementDepuisProcessus = TrouverEmplacementEmulateurDepuisProcessus(definition);

        if (!string.IsNullOrWhiteSpace(emplacementDepuisProcessus))
        {
            return emplacementDepuisProcessus;
        }

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog => TrouverParentSiPossible(
                TrouverRepertoireLogsRetroArch()
            ),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog => TrouverRepertoireDuckStation(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => TrouverRepertoirePCSX2(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => TrouverRepertoirePPSSPP(),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => TrouverParentSiPossible(
                TrouverRepertoireRACacheProject64()
            ),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => TrouverParentSiPossible(
                TrouverRepertoireRACacheRALibretro()
            ),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => TrouverParentSiPossible(
                TrouverRepertoireRACacheRANes()
            ),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => TrouverParentSiPossible(
                TrouverRepertoireRACacheRAVBA()
            ),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => TrouverParentSiPossible(
                TrouverRepertoireRACacheRASnes9x()
            ),
            _ => string.Empty,
        };
    }

    public static bool CheminExecutableCorrespondEmulateur(string nomEmulateur, string cheminExecutable)
    {
        if (string.IsNullOrWhiteSpace(nomEmulateur) || string.IsNullOrWhiteSpace(cheminExecutable))
        {
            return false;
        }

        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null)
        {
            return false;
        }

        try
        {
            string cheminNormalise = Path.GetFullPath(cheminExecutable.Trim());

            if (!File.Exists(cheminNormalise))
            {
                return false;
            }

            FileVersionInfo version = FileVersionInfo.GetVersionInfo(cheminNormalise);
            string[] valeurs =
            [
                Path.GetFileNameWithoutExtension(cheminNormalise),
                cheminNormalise,
                version.ProductName ?? string.Empty,
                version.FileDescription ?? string.Empty,
                version.OriginalFilename ?? string.Empty,
                version.InternalName ?? string.Empty,
            ];

            string[] jetons = ObtenirJetonsCorrespondanceEmulateur(definition);

            return valeurs.Any(valeur => CorrespondValeurEmpreinteEmulateur(valeur, jetons));
        }
        catch
        {
            return false;
        }
    }

    public static string TrouverCheminJournalSuccesLocal(string nomEmulateur)
    {
        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            nomEmulateur
        );

        if (definition is null)
        {
            return string.Empty;
        }

        return definition.StrategieRenseignementJeu switch
        {
            StrategieRenseignementJeuEmulateurLocal.FlycastConfig =>
                TrouverCheminJournalFlycast(),
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => Path.Combine(
                TrouverRepertoireRACacheRALibretro(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => Path.Combine(
                TrouverRepertoireRACacheProject64(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RANesRACache => Path.Combine(
                TrouverRepertoireRACacheRANes(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RAVBARACache => Path.Combine(
                TrouverRepertoireRACacheRAVBA(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RASnes9xRACache => Path.Combine(
                TrouverRepertoireRACacheRASnes9x(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                TrouverDernierCheminJournalRetroArch(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                TrouverCheminJournalDuckStation(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log => TrouverCheminJournalPCSX2(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog => TrouverCheminJournalPPSSPP(),
            _ => string.Empty,
        };
    }

    public static string TrouverRepertoireLogsRetroArch()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string emplacementManuel = ObtenirEmplacementEmulateurManuel("RetroArch");
        string emplacementDetecte = ObtenirEmplacementEmulateurDetecte("RetroArch");
        string emplacementProcessus = TrouverCheminExecutableRetroArchDepuisProcessus();

        string[] candidats =
        [
            TrouverRepertoireLogsDepuisEmplacementRetroArch(emplacementManuel),
            TrouverRepertoireLogsDepuisEmplacementRetroArch(emplacementDetecte),
            TrouverRepertoireLogsDepuisEmplacementRetroArch(emplacementProcessus),
            Path.Combine(documents, "emulation", "RetroArch", "logs"),
            Path.Combine(documents, "RetroArch", "logs"),
            Path.Combine(appData, "RetroArch", "logs"),
        ];

        return candidats
            .Where(candidat => !string.IsNullOrWhiteSpace(candidat))
            .FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverDernierCheminJournalRetroArch()
    {
        try
        {
            string[] candidatsDirects =
            [
                .. ConstruireCandidatsFichiersJournalRetroArch(ObtenirEmplacementEmulateurManuel("RetroArch")),
                .. ConstruireCandidatsFichiersJournalRetroArch(ObtenirEmplacementEmulateurDetecte("RetroArch")),
                .. ConstruireCandidatsFichiersJournalRetroArch(TrouverCheminExecutableRetroArchDepuisProcessus()),
            ];

            string cheminDirect = candidatsDirects.FirstOrDefault(fichier =>
                !string.IsNullOrWhiteSpace(fichier) && File.Exists(fichier)
            ) ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(cheminDirect))
            {
                return cheminDirect;
            }

            string repertoireLogs = TrouverRepertoireLogsRetroArch();

            if (string.IsNullOrWhiteSpace(repertoireLogs) || !Directory.Exists(repertoireLogs))
            {
                return string.Empty;
            }

            DirectoryInfo repertoire = new(repertoireLogs);
            FileInfo? fichierLog = repertoire
                .EnumerateFiles("retroarch__*.log", SearchOption.TopDirectoryOnly)
                .Where(fichier => fichier.Length > 0)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            fichierLog ??= repertoire
                .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                .Where(fichier => fichier.Length > 0)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            return fichierLog?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string TrouverRepertoireRACacheProject64()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "Luna_Project64", "RACache"),
            Path.Combine(documents, "Luna_Project64", "RACache"),
            Path.Combine(appData, "Luna-Project64", "RACache"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireRACacheRALibretro()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RALibretro", "RACache"),
            Path.Combine(documents, "RALibretro", "RACache"),
            Path.Combine(appData, "RALibretro", "RACache"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireRACacheRANes()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RANes-x64", "RACache"),
            Path.Combine(documents, "emulation", "RANes", "RACache"),
            Path.Combine(documents, "RANes-x64", "RACache"),
            Path.Combine(documents, "RANes", "RACache"),
            Path.Combine(appData, "RANes-x64", "RACache"),
            Path.Combine(appData, "RANes", "RACache"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireRACacheRAVBA()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RAVBA-x64", "RACache"),
            Path.Combine(documents, "emulation", "RAVBA", "RACache"),
            Path.Combine(documents, "emulation", "RAVBA-M-x64", "RACache"),
            Path.Combine(documents, "emulation", "RAVBA-M", "RACache"),
            Path.Combine(documents, "RAVBA-x64", "RACache"),
            Path.Combine(documents, "RAVBA", "RACache"),
            Path.Combine(documents, "RAVBA-M-x64", "RACache"),
            Path.Combine(documents, "RAVBA-M", "RACache"),
            Path.Combine(appData, "RAVBA-x64", "RACache"),
            Path.Combine(appData, "RAVBA", "RACache"),
            Path.Combine(appData, "RAVBA-M-x64", "RACache"),
            Path.Combine(appData, "RAVBA-M", "RACache"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireRACacheRASnes9x()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RASnes9x-x64", "RACache"),
            Path.Combine(documents, "emulation", "RASnes9x", "RACache"),
            Path.Combine(documents, "RASnes9x-x64", "RACache"),
            Path.Combine(documents, "RASnes9x", "RACache"),
            Path.Combine(appData, "RASnes9x-x64", "RACache"),
            Path.Combine(appData, "RASnes9x", "RACache"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationRALibretro()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RALibretro", "RALibretro.json"),
            Path.Combine(documents, "RALibretro", "RALibretro.json"),
            Path.Combine(appData, "RALibretro", "RALibretro.json"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationProject64()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "Luna_Project64", "Config", "Project64.cfg"),
            Path.Combine(documents, "Luna_Project64", "Config", "Project64.cfg"),
            Path.Combine(appData, "Luna-Project64", "Config", "Project64.cfg"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationRANes()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RANes-x64", "fceux.cfg"),
            Path.Combine(documents, "emulation", "RANes", "fceux.cfg"),
            Path.Combine(documents, "RANes-x64", "fceux.cfg"),
            Path.Combine(documents, "RANes", "fceux.cfg"),
            Path.Combine(appData, "RANes-x64", "fceux.cfg"),
            Path.Combine(appData, "RANes", "fceux.cfg"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationRASnes9x()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RASnes9x-x64", "snes9x.conf"),
            Path.Combine(documents, "emulation", "RASnes9x", "snes9x.conf"),
            Path.Combine(documents, "RASnes9x-x64", "snes9x.conf"),
            Path.Combine(documents, "RASnes9x", "snes9x.conf"),
            Path.Combine(appData, "RASnes9x-x64", "snes9x.conf"),
            Path.Combine(appData, "RASnes9x", "snes9x.conf"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationRAVBA()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RAVBA-M", "vbam.ini"),
            Path.Combine(documents, "emulation", "RAVBA", "vbam.ini"),
            Path.Combine(documents, "RAVBA-M", "vbam.ini"),
            Path.Combine(documents, "RAVBA", "vbam.ini"),
            Path.Combine(localAppData, "visualboyadvance-m", "vbam.ini"),
            Path.Combine(localAppData, "RAVBA-M", "vbam.ini"),
            Path.Combine(appData, "visualboyadvance-m", "vbam.ini"),
            Path.Combine(appData, "RAVBA-M", "vbam.ini"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireDuckStation()
    {
        string[] candidats =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DuckStation"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DuckStation"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DuckStation"
            ),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoirePCSX2()
    {
        string[] candidats =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PCSX2"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PCSX2"
            ),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoirePPSSPP()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "Playstation Portable"),
            Path.Combine(documents, "PPSSPP"),
            Path.Combine(localAppData, "PPSSPP"),
            Path.Combine(appData, "PPSSPP"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverRepertoireFlycast()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "Dreamcast"),
            Path.Combine(documents, "Flycast"),
            Path.Combine(localAppData, "Flycast"),
            Path.Combine(appData, "Flycast"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverCheminConfigurationFlycast()
    {
        string repertoireFlycast = TrouverRepertoireFlycast();

        string[] candidats =
        [
            Path.Combine(repertoireFlycast, "emu.cfg"),
            Path.Combine(repertoireFlycast, "flycast.cfg"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminJournalFlycast()
    {
        string repertoireFlycast = TrouverRepertoireFlycast();

        string[] candidats =
        [
            Path.Combine(repertoireFlycast, "flycast.log"),
            Path.Combine(repertoireFlycast, "logs", "flycast.log"),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminJournalPCSX2()
    {
        string[] candidats =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PCSX2",
                "logs",
                "emulog.txt"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PCSX2",
                "logs",
                "emulog.txt"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PCSX2",
                "logs",
                "emulog.txt"
            ),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminJournalPPSSPP()
    {
        string repertoirePPSSPP = TrouverRepertoirePPSSPP();

        string[] candidats =
        [
            Path.Combine(repertoirePPSSPP, "memstick", "PSP", "SYSTEM", "DUMP", "log.txt"),
            Path.Combine(repertoirePPSSPP, "log.txt"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "emulation",
                "Playstation Portable",
                "ppsspplog.txt"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PPSSPP",
                "ppsspplog.txt"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PPSSPP",
                "ppsspplog.txt"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PPSSPP",
                "ppsspplog.txt"
            ),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static string TrouverCheminJournalDuckStation()
    {
        string[] candidats =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DuckStation",
                "duckstation.log"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DuckStation",
                "duckstation.log"
            ),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DuckStation",
                "duckstation.log"
            ),
        ];

        return candidats.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public static List<string> LireToutesLesLignesAvecPartage(string cheminFichier)
    {
        try
        {
            using FileStream flux = new(
                cheminFichier,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using StreamReader lecteur = new(
                flux,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );
            List<string> lignes = [];

            while (!lecteur.EndOfStream)
            {
                string? ligne = lecteur.ReadLine();

                if (ligne is not null)
                {
                    lignes.Add(ligne);
                }
            }

            return lignes;
        }
        catch
        {
            return [];
        }
    }

    private static string TrouverEmplacementEmulateurDepuisProcessus(
        DefinitionEmulateurLocal definition
    )
    {
        try
        {
            foreach (Process processus in Process.GetProcesses())
            {
                string cheminExecutable = LireCheminExecutableProcessus(processus);

                if (string.IsNullOrWhiteSpace(cheminExecutable))
                {
                    continue;
                }

                bool correspondAuNom = CorrespondNomProcessus(processus, definition.NomsProcessus);
                bool correspondAuBinaire = CheminExecutableCorrespondEmulateur(
                    definition.NomEmulateur,
                    cheminExecutable
                );

                if ((correspondAuNom || correspondAuBinaire) && File.Exists(cheminExecutable))
                {
                    return cheminExecutable;
                }
            }
        }
        catch
        {
            // Une lecture ponctuelle des processus ne doit pas casser l'aide.
        }

        return string.Empty;
    }

    private static string TrouverCheminExecutableRetroArchDepuisProcessus()
    {
        DefinitionEmulateurLocal? definition = ServiceCatalogueEmulateursLocaux.TrouverParNom(
            "RetroArch"
        );

        return definition is null ? string.Empty : TrouverEmplacementEmulateurDepuisProcessus(definition);
    }

    private static string TrouverRepertoireLogsDepuisEmplacementRetroArch(string emplacement)
    {
        if (string.IsNullOrWhiteSpace(emplacement))
        {
            return string.Empty;
        }

        try
        {
            string repertoireBase = File.Exists(emplacement)
                ? Path.GetDirectoryName(emplacement) ?? string.Empty
                : emplacement;

            if (string.IsNullOrWhiteSpace(repertoireBase))
            {
                return string.Empty;
            }

            string repertoireLogs = Path.Combine(repertoireBase, "logs");
            return Directory.Exists(repertoireLogs) ? repertoireLogs : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IEnumerable<string> ConstruireCandidatsFichiersJournalRetroArch(string emplacement)
    {
        if (string.IsNullOrWhiteSpace(emplacement))
        {
            yield break;
        }

        string repertoireBase;

        try
        {
            repertoireBase = File.Exists(emplacement)
                ? Path.GetDirectoryName(emplacement) ?? string.Empty
                : emplacement;
        }
        catch
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(repertoireBase))
        {
            yield break;
        }

        yield return Path.Combine(repertoireBase, "logs", "retroarch.log");
        yield return Path.Combine(repertoireBase, "retroarch.log");
    }

    private static bool CorrespondNomProcessus(Process processus, IReadOnlyList<string> nomsProcessus)
    {
        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;

        return nomsProcessus.Any(nom =>
            string.Equals(nomProcessus, nom, StringComparison.OrdinalIgnoreCase)
            || nomProcessus.StartsWith(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string LireCheminExecutableProcessus(Process processus)
    {
        try
        {
            return processus.MainModule?.FileName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool CorrespondValeurEmpreinteEmulateur(
        string valeur,
        IReadOnlyList<string> jetons
    )
    {
        string valeurNormalisee = NormaliserEmpreinteExecutable(valeur);

        if (string.IsNullOrWhiteSpace(valeurNormalisee))
        {
            return false;
        }

        return jetons.Any(jeton =>
        {
            string jetonNormalise = NormaliserEmpreinteExecutable(jeton);
            return !string.IsNullOrWhiteSpace(jetonNormalise)
                && valeurNormalisee.Contains(jetonNormalise, StringComparison.Ordinal);
        });
    }

    private static string[] ObtenirJetonsCorrespondanceEmulateur(
        DefinitionEmulateurLocal definition
    )
    {
        List<string> jetons = [definition.NomEmulateur, .. definition.NomsProcessus];

        if (string.Equals(definition.NomEmulateur, "RAVBA", StringComparison.Ordinal))
        {
            jetons.Add("VisualBoyAdvance");
            jetons.Add("VisualBoyAdvance-M");
        }
        else if (string.Equals(definition.NomEmulateur, "RASnes9x", StringComparison.Ordinal))
        {
            jetons.Add("Snes9x");
        }
        else if (string.Equals(definition.NomEmulateur, "LunaProject64", StringComparison.Ordinal))
        {
            jetons.Add("Project64");
            jetons.Add("Luna Project64");
        }

        return [.. jetons.Where(jeton => !string.IsNullOrWhiteSpace(jeton)).Distinct()];
    }

    private static string NormaliserEmpreinteExecutable(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        StringBuilder builder = new(valeur.Length);

        foreach (char caractere in valeur)
        {
            if (char.IsLetterOrDigit(caractere))
            {
                builder.Append(char.ToLowerInvariant(caractere));
            }
        }

        return builder.ToString();
    }

    private static string TrouverParentSiPossible(string chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin))
        {
            return string.Empty;
        }

        try
        {
            DirectoryInfo? repertoire = Directory.Exists(chemin)
                ? new DirectoryInfo(chemin)
                : File.Exists(chemin)
                    ? new FileInfo(chemin).Directory
                    : null;

            if (repertoire?.Parent is not null)
            {
                return repertoire.Parent.FullName;
            }

            return repertoire?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
