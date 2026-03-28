using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte localement les principaux émulateurs connus à partir des processus et titres de fenêtre.
/// </summary>
public sealed class ServiceSondeLocaleEmulateurs
{
    private sealed record DefinitionEmulateur(
        string NomEmulateur,
        string[] NomsProcessus,
        Func<string, string> ExtraireTitreJeu
    );

    private static readonly DefinitionEmulateur[] Definitions =
    [
        new(
            "RetroArch",
            ["retroarch"],
            titre => ExtraireTitreAvecSeparateurs(titre, "RetroArch", "RetroArch ")
        ),
        new(
            "DuckStation",
            ["duckstation", "duckstation-qt"],
            titre => ExtraireTitreAvecSeparateurs(titre, "DuckStation", "DuckStation ")
        ),
        new("PCSX2", ["pcsx2", "pcsx2-qt"], titre => ExtraireTitreAvecSeparateurs(titre, "PCSX2")),
        new(
            "PPSSPP",
            ["ppsspp", "ppssppwindows", "ppssppwindows64"],
            titre => ExtraireTitreAvecSeparateurs(titre, "PPSSPP")
        ),
        new(
            "Dolphin",
            ["dolphin", "slippi dolphin"],
            titre => ExtraireTitreAvecSeparateurs(titre, "Dolphin", "Slippi Dolphin")
        ),
        new("Flycast", ["flycast"], titre => ExtraireTitreAvecSeparateurs(titre, "Flycast")),
        new(
            "Project64",
            ["project64"],
            titre => ExtraireTitreAvecSeparateurs(titre, "Project64", "Project 64")
        ),
    ];

    private static readonly string CheminJournalSondeLocale = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-sonde-locale.log"
    );
    private string _derniereSignatureJournalisee = "\u0000";

    public static void ReinitialiserJournalSession()
    {
        try
        {
            string? repertoire = Path.GetDirectoryName(CheminJournalSondeLocale);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.WriteAllText(
                CheminJournalSondeLocale,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
        }
        catch
        {
            // Cette journalisation reste purement auxiliaire.
        }
    }

    public EtatSondeLocaleEmulateur Sonder(bool journaliser = true)
    {
        try
        {
            Process[] processus = Process.GetProcesses();

            foreach (DefinitionEmulateur definition in Definitions)
            {
                EtatSondeLocaleEmulateur? etat = SonderPourDefinition(definition, processus);

                if (etat is not null)
                {
                    if (journaliser)
                    {
                        JournaliserSiChangement(etat);
                    }

                    return etat;
                }
            }
        }
        catch
        {
            // Une erreur ponctuelle de lecture des processus ne doit pas casser l'application.
        }

        EtatSondeLocaleEmulateur etatAucun = new()
        {
            EmulateurDetecte = false,
            Signature = string.Empty,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };

        if (journaliser)
        {
            JournaliserSiChangement(etatAucun);
        }

        return etatAucun;
    }

    private static EtatSondeLocaleEmulateur? SonderPourDefinition(
        DefinitionEmulateur definition,
        IEnumerable<Process> processus
    )
    {
        Process? processusCible = processus
            .Where(processusCourant => Correspond(processusCourant.ProcessName, definition))
            .OrderByDescending(ProcessusPossedeUneFenetreVisible)
            .ThenByDescending(processusCourant => LireTitreFenetre(processusCourant).Length)
            .FirstOrDefault();

        if (processusCible is null)
        {
            return null;
        }

        string titreFenetre = LireTitreFenetre(processusCible);
        string titreJeuProbable = definition.ExtraireTitreJeu(titreFenetre);
        string signature =
            $"{definition.NomEmulateur}|{processusCible.ProcessName}|{titreFenetre}|{titreJeuProbable}";

        return new EtatSondeLocaleEmulateur
        {
            EmulateurDetecte = true,
            NomEmulateur = definition.NomEmulateur,
            NomProcessus = processusCible.ProcessName,
            TitreFenetre = titreFenetre,
            TitreJeuProbable = titreJeuProbable,
            Signature = signature,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    private static bool Correspond(string nomProcessus, DefinitionEmulateur definition)
    {
        return definition.NomsProcessus.Any(nom =>
            string.Equals(nomProcessus, nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static int ProcessusPossedeUneFenetreVisible(Process processus)
    {
        try
        {
            processus.Refresh();
            return processus.MainWindowHandle != IntPtr.Zero ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string LireTitreFenetre(Process processus)
    {
        try
        {
            processus.Refresh();
            return processus.MainWindowTitle?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtraireTitreAvecSeparateurs(
        string titreFenetre,
        params string[] jetonsEmulateur
    )
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string[] separateurs = [" - ", " | ", " — ", " – ", " :: ", " / "];

        foreach (string separateur in separateurs)
        {
            string[] morceaux = titre.Split(separateur, StringSplitOptions.RemoveEmptyEntries);

            if (morceaux.Length < 2)
            {
                continue;
            }

            string premier = morceaux[0].Trim();
            string dernier = morceaux[^1].Trim();

            if (ContientJetonEmulateur(dernier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(premier, jetonsEmulateur);
            }

            if (ContientJetonEmulateur(premier, jetonsEmulateur))
            {
                return NettoyerTitreJeu(dernier, jetonsEmulateur);
            }
        }

        if (ContientJetonEmulateur(titre, jetonsEmulateur))
        {
            return string.Empty;
        }

        return NettoyerTitreJeu(titre, jetonsEmulateur);
    }

    private static bool ContientJetonEmulateur(string valeur, IEnumerable<string> jetonsEmulateur)
    {
        return jetonsEmulateur.Any(jeton =>
            valeur.Contains(jeton, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static string NettoyerTitreJeu(string titre, IEnumerable<string> jetonsEmulateur)
    {
        string resultat = titre.Trim();

        foreach (string jeton in jetonsEmulateur)
        {
            resultat = Regex.Replace(
                resultat,
                $@"\b{Regex.Escape(jeton)}\b",
                string.Empty,
                RegexOptions.IgnoreCase
            );
        }

        resultat = Regex.Replace(resultat, @"\s+", " ").Trim();
        return resultat;
    }

    private void JournaliserSiChangement(EtatSondeLocaleEmulateur etat)
    {
        if (string.Equals(_derniereSignatureJournalisee, etat.Signature, StringComparison.Ordinal))
        {
            return;
        }

        _derniereSignatureJournalisee = etat.Signature;
        Journaliser(etat);
    }

    private static void Journaliser(EtatSondeLocaleEmulateur etat)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalSondeLocale)!);
            File.AppendAllText(
                CheminJournalSondeLocale,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] detecte={etat.EmulateurDetecte};emulateur={NettoyerPourJournal(etat.NomEmulateur)};processus={NettoyerPourJournal(etat.NomProcessus)};titreFenetre={NettoyerPourJournal(etat.TitreFenetre)};titreJeu={NettoyerPourJournal(etat.TitreJeuProbable)};signature={NettoyerPourJournal(etat.Signature)}{Environment.NewLine}"
                )
            );
        }
        catch
        {
            // Cette journalisation reste purement auxiliaire.
        }
    }

    private static string NettoyerPourJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
