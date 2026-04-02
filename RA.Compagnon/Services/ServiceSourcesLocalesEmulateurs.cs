using System.Diagnostics;
using System.IO;
using System.Text;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public static class ServiceSourcesLocalesEmulateurs
{
    private static readonly Lock VerrouEmplacementsEmulateursManuels = new();
    private static Dictionary<string, string> _emplacementsEmulateursManuels = [];

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

        string[] candidats =
        [
            Path.Combine(documents, "emulation", "RetroArch", "logs"),
            Path.Combine(documents, "RetroArch", "logs"),
            Path.Combine(appData, "RetroArch", "logs"),
        ];

        return candidats.FirstOrDefault(Directory.Exists) ?? string.Empty;
    }

    public static string TrouverDernierCheminJournalRetroArch()
    {
        try
        {
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
                if (!CorrespondNomProcessus(processus, definition.NomsProcessus))
                {
                    continue;
                }

                string cheminExecutable = LireCheminExecutableProcessus(processus);

                if (string.IsNullOrWhiteSpace(cheminExecutable))
                {
                    continue;
                }

                if (File.Exists(cheminExecutable))
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
