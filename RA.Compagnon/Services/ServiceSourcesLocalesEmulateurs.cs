using System.IO;
using System.Text;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public static class ServiceSourcesLocalesEmulateurs
{
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
            StrategieRenseignementJeuEmulateurLocal.RALibretroRACache => Path.Combine(
                TrouverRepertoireRACacheRALibretro(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.Project64RACache => Path.Combine(
                TrouverRepertoireRACacheProject64(),
                "RALog.txt"
            ),
            StrategieRenseignementJeuEmulateurLocal.RetroArchLog =>
                TrouverDernierCheminJournalRetroArch(),
            StrategieRenseignementJeuEmulateurLocal.DuckStationLog =>
                TrouverCheminJournalDuckStation(),
            StrategieRenseignementJeuEmulateurLocal.PCSX2Log =>
                TrouverCheminJournalPCSX2(),
            StrategieRenseignementJeuEmulateurLocal.PPSSPPLog =>
                TrouverCheminJournalPPSSPP(),
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
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PCSX2"
            ),
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
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
}
