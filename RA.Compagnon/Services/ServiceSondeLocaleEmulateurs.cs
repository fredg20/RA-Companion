using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte localement les principaux émulateurs connus à partir des processus et titres de fenêtre.
/// </summary>
public sealed partial class ServiceSondeLocaleEmulateurs
{
    private sealed record RenseignementJeuRA(int IdentifiantJeu, string TitreJeu);

    public sealed record RenseignementSuccesRA(int IdentifiantSucces, string TitreSucces);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int ProcessCommandLineInformation = 60;
    private static readonly Lock VerrouCacheDuckStation = new();
    private static readonly Lock VerrouCacheRALibretro = new();
    private static readonly Lock VerrouCacheRetroArch = new();
    private static string _dernierRepertoireDuckStation = string.Empty;
    private static DateTime _dernierHorodatageCacheGamelistUtc = DateTime.MinValue;
    private static Dictionary<string, string> _cacheSerialVersCheminDuckStation = [];
    private static RenseignementJeuRA? _dernierRenseignementRALibretro;
    private static DateTime _dernierHorodatageRenseignementRALibretroUtc = DateTime.MinValue;
    private static RenseignementJeuRA? _dernierRenseignementRetroArch;
    private static DateTime _dernierHorodatageRenseignementRetroArchUtc = DateTime.MinValue;

    private sealed record DefinitionEmulateur(
        string NomEmulateur,
        string[] NomsProcessus,
        Func<Process, string, string> ExtraireTitreJeu
    );

    private static readonly DefinitionEmulateur[] Definitions =
    [
        new(
            "RetroArch",
            ["retroarch"],
            (_, titre) => ExtraireTitreAvecSeparateurs(titre, "RetroArch", "RetroArch ")
        ),
        new("RALibretro", ["ralibretro"], ExtraireTitreRALibretro),
        new(
            "DuckStation",
            ["duckstation", "duckstation-qt", "duckstation-nogui", "duckstation-sdl"],
            ExtraireTitreDuckStation
        ),
        new("PCSX2", ["pcsx2", "pcsx2-qt"], ExtraireTitrePCSX2),
        new("PPSSPP", ["ppsspp", "ppssppwindows", "ppssppwindows64"], ExtraireTitrePPSSPP),
        new(
            "Dolphin",
            [
                "dolphin",
                "dolphin-qt2",
                "dolphin emulator",
                "slippi dolphin",
                "slippi dolphin launcher",
            ],
            ExtraireTitreDolphin
        ),
        new("LunaProject64", ["project64"], ExtraireTitreProject64),
        new("Flycast", ["flycast"], (_, titre) => ExtraireTitreAvecSeparateurs(titre, "Flycast")),
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

    public static bool SonderPresenceEmulateur()
    {
        try
        {
            Process[] processus = Process.GetProcesses();
            return Definitions.Any(definition =>
                processus.Any(processusCourant =>
                    CorrespondNomProcessus(processusCourant, definition)
                )
            );
        }
        catch
        {
            return false;
        }
    }

    private static EtatSondeLocaleEmulateur? SonderPourDefinition(
        DefinitionEmulateur definition,
        IEnumerable<Process> processus
    )
    {
        Process? processusCible = processus
            .Where(processusCourant => Correspond(processusCourant, definition))
            .OrderByDescending(ProcessusPossedeUneFenetreVisible)
            .ThenByDescending(processusCourant => LireTitreFenetre(processusCourant).Length)
            .FirstOrDefault();

        if (processusCible is null)
        {
            return null;
        }

        IReadOnlyList<string> titresFenetres = LireTitresFenetresVisibles(processusCible);
        string titreFenetre = ChoisirTitreFenetre(definition, processusCible, titresFenetres);
        string titreJeuProbable = definition.ExtraireTitreJeu(processusCible, titreFenetre);
        int identifiantJeuProbable = 0;
        string informationsDiagnostic = string.Empty;

        if (string.Equals(definition.NomEmulateur, "LunaProject64", StringComparison.Ordinal))
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuProject64DepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic =
                    $"racacheGameId={identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}";
            }
        }
        else if (string.Equals(definition.NomEmulateur, "RALibretro", StringComparison.Ordinal))
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic =
                    $"racacheGameId={identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}";
            }

            if (identifiantJeuProbable <= 0)
            {
                titreJeuProbable = string.Empty;
            }
        }
        else if (string.Equals(definition.NomEmulateur, "RetroArch", StringComparison.Ordinal))
        {
            RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRetroArchDepuisLog();

            if (renseignementJeu is not null)
            {
                identifiantJeuProbable = renseignementJeu.IdentifiantJeu;

                if (!string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
                {
                    titreJeuProbable = renseignementJeu.TitreJeu;
                }

                informationsDiagnostic =
                    $"logGameId={identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}";
            }
        }

        if (
            string.Equals(definition.NomEmulateur, "DuckStation", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(titreJeuProbable)
        )
        {
            informationsDiagnostic = ConstruireDiagnosticDuckStation(
                processusCible,
                titresFenetres
            );
        }

        string signature =
            $"{definition.NomEmulateur}|{processusCible.ProcessName}|{titreFenetre}|{titreJeuProbable}|{identifiantJeuProbable.ToString(CultureInfo.InvariantCulture)}|{informationsDiagnostic}";

        return new EtatSondeLocaleEmulateur
        {
            EmulateurDetecte = true,
            NomEmulateur = definition.NomEmulateur,
            NomProcessus = processusCible.ProcessName,
            TitreFenetre = titreFenetre,
            TitreJeuProbable = titreJeuProbable,
            IdentifiantJeuProbable = identifiantJeuProbable,
            InformationsDiagnostic = informationsDiagnostic,
            Signature = signature,
            HorodatageUtc = DateTimeOffset.UtcNow,
        };
    }

    private static bool Correspond(Process processus, DefinitionEmulateur definition)
    {
        bool correspondAuNomProcessus = CorrespondNomProcessus(processus, definition);

        if (correspondAuNomProcessus)
        {
            return true;
        }

        // RetroArch, DuckStation et PCSX2 ont des variantes de fenêtres/outils qui rendent
        // le fallback par titre trop bruyant (explorer, navigateurs, installateur, dialogues internes, etc.).
        if (
            string.Equals(definition.NomEmulateur, "RetroArch", StringComparison.Ordinal)
            || string.Equals(definition.NomEmulateur, "DuckStation", StringComparison.Ordinal)
            || string.Equals(definition.NomEmulateur, "PCSX2", StringComparison.Ordinal)
        )
        {
            return false;
        }

        string titreFenetre = LireTitreFenetre(processus);

        return !string.IsNullOrWhiteSpace(titreFenetre)
            && titreFenetre.StartsWith(definition.NomEmulateur, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CorrespondNomProcessus(Process processus, DefinitionEmulateur definition)
    {
        string nomProcessus = processus.ProcessName?.Trim() ?? string.Empty;
        return definition.NomsProcessus.Any(nom =>
            string.Equals(nomProcessus, nom, StringComparison.OrdinalIgnoreCase)
            || nomProcessus.StartsWith(nom, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static int ProcessusPossedeUneFenetreVisible(Process processus)
    {
        try
        {
            return LireTitresFenetresVisibles(processus).Count;
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
            return ChoisirTitreFenetre(
                new DefinitionEmulateur(string.Empty, [], (_, titre) => titre),
                processus,
                LireTitresFenetresVisibles(processus)
            );
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

        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    private static string ExtraireTitreDuckStation(Process processus, string titreFenetre)
    {
        string titreFenetreExtrait = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "DuckStation",
            "DuckStation "
        );

        if (!string.IsNullOrWhiteSpace(titreFenetreExtrait))
        {
            return titreFenetreExtrait;
        }

        string titreAutomatisation = ExtraireTitreDuckStationDepuisAutomatisation(processus);

        if (!string.IsNullOrWhiteSpace(titreAutomatisation))
        {
            return titreAutomatisation;
        }

        string ligneCommande = LireLigneCommandeProcessus(processus);
        string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

        if (!string.IsNullOrWhiteSpace(cheminJeu))
        {
            return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
        }

        string titreDepuisMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

        if (!string.IsNullOrWhiteSpace(titreDepuisMemcard))
        {
            return titreDepuisMemcard;
        }

        return string.Empty;
    }

    private static string ExtraireTitrePCSX2(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PCSX2");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialoguePCSX2(titreNettoye))
        {
            return string.Empty;
        }

        return titreNettoye;
    }

    private static string ExtraireTitrePPSSPP(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(titreFenetre, "PPSSPP");

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        // PPSSPP affiche souvent le serial PSP devant le vrai titre.
        titreNettoye = PrefixeSerialPpssppRegex().Replace(titreNettoye, string.Empty).Trim();

        titreNettoye = titreNettoye.Replace("Â®", string.Empty, StringComparison.Ordinal);
        titreNettoye = titreNettoye.Replace("®", string.Empty, StringComparison.Ordinal);
        titreNettoye = EspacesMultiplesRegex().Replace(titreNettoye, " ").Trim();

        return titreNettoye;
    }

    private static string ExtraireTitreDolphin(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreAvecSeparateurs(
            titreFenetre,
            "Dolphin",
            "Dolphin Emulator",
            "Slippi Dolphin",
            "Slippi Dolphin Launcher"
        );

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string titreNettoye = titre.Trim();

        if (EstDialogueDolphin(titreNettoye))
        {
            return string.Empty;
        }

        titreNettoye = SuffixeCodeJeuDolphinRegex().Replace(titreNettoye, string.Empty).Trim();

        return titreNettoye;
    }

    private static string ExtraireTitreProject64(Process _, string titreFenetre)
    {
        return ExtraireTitreProject64DepuisFenetre(titreFenetre);
    }

    private static string ExtraireTitreRALibretro(Process _, string titreFenetre)
    {
        string titre = ExtraireTitreRALibretroDepuisFenetre(titreFenetre);

        if (!string.IsNullOrWhiteSpace(titre))
        {
            return titre;
        }

        RenseignementJeuRA? renseignementJeu = LireRenseignementJeuRALibretroDepuisRACache();

        if (renseignementJeu is not null && !string.IsNullOrWhiteSpace(renseignementJeu.TitreJeu))
        {
            return renseignementJeu.TitreJeu;
        }

        string cheminConfiguration = TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                document.RootElement.TryGetProperty("recent", out JsonElement recent)
                && recent.ValueKind == JsonValueKind.Array
                && recent.GetArrayLength() > 0
                && recent[0].TryGetProperty("path", out JsonElement path)
                && path.ValueKind == JsonValueKind.String
            )
            {
                string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }
        }
        catch
        {
            // Le JSON local reste un fallback opportuniste.
        }

        return string.Empty;
    }

    private static string ExtraireTitreRALibretroDepuisFenetre(string titreFenetre)
    {
        string titre = titreFenetre.Trim();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        if (
            titre.StartsWith("RALibretro", StringComparison.OrdinalIgnoreCase)
            || titre.StartsWith("RALibRetro", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Les fenetres RALibretro exposent surtout version/core/system/profil.
            // Le nom du jeu fiable vient du RACache ou du JSON recent.
            return string.Empty;
        }

        return NettoyerNomFichierJeu(titre);
    }

    private static bool EstDialogueDolphin(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = titre.Trim().ToLowerInvariant();

        return titreNormalise
            is "selectionner un dossier"
                or "sélectionner un dossier"
                or "select a folder"
                or "select a directory"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    private static bool EstDialoguePCSX2(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = NormaliserTexteComparaison(titre);

        if (titreNormalise.StartsWith("pcsx2 v", StringComparison.Ordinal))
        {
            return true;
        }

        if (titreNormalise.StartsWith("pcsx2 update installer", StringComparison.Ordinal))
        {
            return true;
        }

        return titreNormalise
            is "mise a jour automatique"
                or "lancer un disque"
                or "telecharger des jaquettes"
                or "parametres pcsx2"
                or "selectionner un dossier"
                or "scanner les sous-dossiers ?"
                or "confirmer l'extinction"
                or "attention : memory card occupee"
                or "ouvrir"
                or "open"
                or "browse for folder";
    }

    private static string ExtraireTitreProject64DepuisFenetre(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string[] morceaux = titreFenetre.Split(
            " - ",
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

        if (
            morceaux.Length >= 3
            && morceaux[0].Contains("Project64", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (EstBlocVersionProject64(morceaux[1]))
            {
                if (morceaux.Length == 3)
                {
                    // Format "LunaProject64 - 3.6 - Profil" : aucun jeu exploitable n'est encore visible.
                    return string.Empty;
                }

                string candidat = morceaux.Length >= 4 ? morceaux[^2] : morceaux[^1];
                return NettoyerTitreJeu(candidat, ["Project64", "LunaProject64"]);
            }

            return NettoyerTitreJeu(morceaux[^1], ["Project64", "LunaProject64"]);
        }

        return ExtraireTitreAvecSeparateurs(titreFenetre, "Project64", "LunaProject64");
    }

    private static bool EstBlocVersionProject64(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        return BlocVersionProject64Regex().IsMatch(valeur.Trim());
    }

    private static string ExtraireTitreDuckStationDepuisAutomatisation(Process processus)
    {
        try
        {
            processus.Refresh();

            if (processus.MainWindowHandle == IntPtr.Zero)
            {
                return string.Empty;
            }

            AutomationElement fenetre = AutomationElement.FromHandle(processus.MainWindowHandle);
            AutomationElementCollection elements = fenetre.FindAll(
                TreeScope.Descendants,
                Condition.TrueCondition
            );

            string meilleurNom = string.Empty;
            double meilleurScore = 0;

            foreach (AutomationElement element in elements.Cast<AutomationElement>())
            {
                string nom = element.Current.Name?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(nom))
                {
                    continue;
                }

                double score = EvaluerNomAutomatisationDuckStation(nom);

                if (score > meilleurScore)
                {
                    meilleurScore = score;
                    meilleurNom = nom;
                }
            }

            return meilleurScore >= 1.2 ? meilleurNom : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static double EvaluerNomAutomatisationDuckStation(string nom)
    {
        string valeur = nom.Trim();

        if (string.IsNullOrWhiteSpace(valeur))
        {
            return 0;
        }

        string valeurMinuscule = valeur.ToLowerInvariant();

        string[] termesExclus =
        [
            "duckstation",
            "file",
            "emulation",
            "system",
            "settings",
            "tools",
            "view",
            "help",
            "debug",
            "fullscreen",
            "pause",
            "resume",
            "controller",
            "memory card",
            "game list",
            "toolbar",
            "status bar",
            "qt",
        ];

        if (termesExclus.Any(terme => valeurMinuscule.Contains(terme, StringComparison.Ordinal)))
        {
            return 0;
        }

        if (ValeurNumeriqueSeuleRegex().IsMatch(valeurMinuscule))
        {
            return 0;
        }

        double score = 0;

        if (valeur.Length >= 4)
        {
            score += 0.4;
        }

        if (valeur.Contains(' '))
        {
            score += 0.4;
        }

        if (ContientDeuxLettresRegex().IsMatch(valeur))
        {
            score += 0.4;
        }

        if (valeur.Length >= 8)
        {
            score += 0.2;
        }

        return score;
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
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] detecte={etat.EmulateurDetecte};emulateur={NettoyerPourJournal(etat.NomEmulateur)};processus={NettoyerPourJournal(etat.NomProcessus)};titreFenetre={NettoyerPourJournal(etat.TitreFenetre)};titreJeu={NettoyerPourJournal(etat.TitreJeuProbable)};gameId={etat.IdentifiantJeuProbable.ToString(CultureInfo.InvariantCulture)};diagnostic={NettoyerPourJournal(etat.InformationsDiagnostic)};signature={NettoyerPourJournal(etat.Signature)}{Environment.NewLine}"
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

    private static IReadOnlyList<string> LireTitresFenetresVisibles(Process processus)
    {
        List<string> titres = [];

        try
        {
            EnumWindows(
                (handle, _) =>
                {
                    if (!IsWindowVisible(handle))
                    {
                        return true;
                    }

                    if (GetWindowThreadProcessId(handle, out uint identifiantProcessusFenetre) == 0)
                    {
                        return true;
                    }

                    if (identifiantProcessusFenetre != unchecked((uint)processus.Id))
                    {
                        return true;
                    }

                    int longueur = GetWindowTextLength(handle);

                    if (longueur <= 0)
                    {
                        return true;
                    }

                    StringBuilder constructeur = new(longueur + 1);
                    _ = GetWindowText(handle, constructeur, constructeur.Capacity);
                    string titre = constructeur.ToString().Trim();

                    if (!string.IsNullOrWhiteSpace(titre))
                    {
                        titres.Add(titre);
                    }

                    return true;
                },
                IntPtr.Zero
            );
        }
        catch
        {
            return [];
        }

        return [.. titres.Distinct(StringComparer.Ordinal)];
    }

    private static string ChoisirTitreFenetre(
        DefinitionEmulateur definition,
        Process processus,
        IReadOnlyList<string> titres
    )
    {
        if (titres.Count > 0)
        {
            if (string.Equals(definition.NomEmulateur, "PCSX2", StringComparison.Ordinal))
            {
                string? titreJeu = titres.FirstOrDefault(titre =>
                    !string.IsNullOrWhiteSpace(ExtraireTitrePCSX2(processus, titre))
                );

                if (!string.IsNullOrWhiteSpace(titreJeu))
                {
                    return titreJeu;
                }
            }

            return titres
                .OrderByDescending(titre => titre.Length)
                .ThenByDescending(titre => titre.Contains(" - ", StringComparison.Ordinal))
                .First();
        }

        processus.Refresh();
        return processus.MainWindowTitle?.Trim() ?? string.Empty;
    }

    private static string NormaliserTexteComparaison(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return string.Empty;
        }

        string normalise = valeur.Normalize(NormalizationForm.FormD);
        StringBuilder constructeur = new(normalise.Length);

        foreach (char caractere in normalise)
        {
            UnicodeCategory categorie = CharUnicodeInfo.GetUnicodeCategory(caractere);

            if (categorie != UnicodeCategory.NonSpacingMark)
            {
                constructeur.Append(caractere);
            }
        }

        return constructeur.ToString().Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    }

    private static string NormaliserTitreComparaisonSouple(string valeur)
    {
        string normalise = NormaliserTexteComparaison(valeur);

        if (string.IsNullOrWhiteSpace(normalise))
        {
            return string.Empty;
        }

        normalise = TexteEntreParenthesesRegex().Replace(normalise, " ");
        normalise = CaracteresNonAlphaNumeriquesRegex().Replace(normalise, " ");
        return EspacesMultiplesRegex().Replace(normalise, " ").Trim();
    }

    private static bool TitresSemblables(string titreA, string titreB)
    {
        string normaliseA = NormaliserTitreComparaisonSouple(titreA);
        string normaliseB = NormaliserTitreComparaisonSouple(titreB);

        if (string.IsNullOrWhiteSpace(normaliseA) || string.IsNullOrWhiteSpace(normaliseB))
        {
            return false;
        }

        return string.Equals(normaliseA, normaliseB, StringComparison.Ordinal)
            || normaliseA.Contains(normaliseB, StringComparison.Ordinal)
            || normaliseB.Contains(normaliseA, StringComparison.Ordinal);
    }

    private static string ConstruireDiagnosticDuckStation(
        Process processus,
        IReadOnlyList<string> titresFenetres
    )
    {
        try
        {
            List<string> morceaux = [];

            if (titresFenetres.Count > 0)
            {
                morceaux.Add($"titres=[{string.Join(" | ", titresFenetres)}]");
            }

            if (processus.MainWindowHandle != IntPtr.Zero)
            {
                AutomationElement fenetre = AutomationElement.FromHandle(
                    processus.MainWindowHandle
                );
                AutomationElementCollection elements = fenetre.FindAll(
                    TreeScope.Descendants,
                    Condition.TrueCondition
                );

                string[] noms =
                [
                    .. elements
                        .Cast<AutomationElement>()
                        .Select(element => element.Current.Name?.Trim() ?? string.Empty)
                        .Where(nom => !string.IsNullOrWhiteSpace(nom))
                        .Distinct(StringComparer.Ordinal)
                        .Take(12),
                ];

                if (noms.Length > 0)
                {
                    morceaux.Add($"ui=[{string.Join(" | ", noms)}]");
                }
            }

            string ligneCommande = LireLigneCommandeProcessus(processus);

            if (!string.IsNullOrWhiteSpace(ligneCommande))
            {
                string cheminJeu = ExtraireCheminJeuDepuisLigneCommande(ligneCommande);

                morceaux.Add(
                    string.IsNullOrWhiteSpace(cheminJeu)
                        ? $"cmd=[{ligneCommande}]"
                        : $"cmdJeu=[{cheminJeu}]"
                );
            }

            string titreMemcard = ExtraireTitreDuckStationDepuisMemcardRecente();

            if (!string.IsNullOrWhiteSpace(titreMemcard))
            {
                morceaux.Add($"memcardJeu=[{titreMemcard}]");
            }

            return string.Join("; ", morceaux);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string LireLigneCommandeProcessus(Process processus)
    {
        try
        {
            int statut = NtQueryInformationProcess(
                processus.Handle,
                ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out int tailleRetour
            );

            if (tailleRetour <= 0 && statut != unchecked((int)0xC0000004))
            {
                return string.Empty;
            }

            IntPtr buffer = Marshal.AllocHGlobal(tailleRetour);

            try
            {
                statut = NtQueryInformationProcess(
                    processus.Handle,
                    ProcessCommandLineInformation,
                    buffer,
                    tailleRetour,
                    out tailleRetour
                );

                if (statut < 0)
                {
                    return string.Empty;
                }

                UnicodeString commande = Marshal.PtrToStructure<UnicodeString>(buffer);

                if (commande.Length <= 0 || commande.Buffer == IntPtr.Zero)
                {
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(commande.Buffer, commande.Length / 2)?.Trim()
                    ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtraireCheminJeuDepuisLigneCommande(string ligneCommande)
    {
        if (string.IsNullOrWhiteSpace(ligneCommande))
        {
            return string.Empty;
        }

        MatchCollection correspondances = JetonsLigneCommandeRegex().Matches(ligneCommande);

        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".exe",
        ];

        foreach (Match correspondance in correspondances.Cast<Match>().Reverse())
        {
            string valeur = correspondance.Groups[1].Success
                ? correspondance.Groups[1].Value
                : correspondance.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(valeur) || valeur.StartsWith('-'))
            {
                continue;
            }

            string extension = Path.GetExtension(valeur);

            if (
                !string.IsNullOrWhiteSpace(extension)
                && extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase)
            )
            {
                return valeur.Trim();
            }
        }

        return string.Empty;
    }

    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisLog()
    {
        try
        {
            string cheminJournal = TrouverDernierCheminJournalRetroArch();

            if (string.IsNullOrWhiteSpace(cheminJournal))
            {
                return null;
            }

            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(cheminJournal) > TimeSpan.FromMinutes(15))
            {
                return LireRenseignementJeuRetroArchDepuisCache();
            }

            foreach (
                string ligne in LireToutesLesLignesAvecPartage(cheminJournal)
                    .AsEnumerable()
                    .Reverse()
            )
            {
                Match correspondanceIdentifie = RetroArchGameIdentifieRegex().Match(ligne);

                if (
                    correspondanceIdentifie.Success
                    && int.TryParse(
                        correspondanceIdentifie.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuIdentifie
                    )
                )
                {
                    string titreJeu = correspondanceIdentifie.Groups[2].Value.Trim();
                    return MemoriserRenseignementRetroArch(
                        new RenseignementJeuRA(identifiantJeuIdentifie, titreJeu)
                    );
                }

                Match correspondanceSession = RetroArchSessionJeuRegex().Match(ligne);

                if (
                    correspondanceSession.Success
                    && int.TryParse(
                        correspondanceSession.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeuSession
                    )
                )
                {
                    return MemoriserRenseignementRetroArch(
                        new RenseignementJeuRA(identifiantJeuSession, string.Empty)
                    );
                }
            }
        }
        catch
        {
            // Le log RetroArch reste une aide locale facultative.
        }

        return LireRenseignementJeuRetroArchDepuisCache();
    }

    private static string TrouverDernierCheminJournalRetroArch()
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

    private static RenseignementJeuRA? MemoriserRenseignementRetroArch(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRetroArch)
        {
            _dernierRenseignementRetroArch = renseignement;
            _dernierHorodatageRenseignementRetroArchUtc = DateTime.UtcNow;
            return _dernierRenseignementRetroArch;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRetroArchDepuisCache()
    {
        lock (VerrouCacheRetroArch)
        {
            if (
                _dernierRenseignementRetroArch is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRetroArchUtc
                    > TimeSpan.FromSeconds(10)
            )
            {
                return null;
            }

            return _dernierRenseignementRetroArch;
        }
    }

    private static List<string> LireToutesLesLignesAvecPartage(string cheminFichier)
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

    private static string TrouverRepertoireLogsRetroArch()
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

    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisRACache()
    {
        try
        {
            string repertoireRACache = TrouverRepertoireRACacheProject64();

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return null;
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");
            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        return LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);
                    }
                }
            }

            return LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisRACache()
    {
        try
        {
            string repertoireRACache = TrouverRepertoireRACacheRALibretro();
            RenseignementJeuRA? renseignementConfiguration =
                LireRenseignementJeuRALibretroDepuisConfiguration();

            if (string.IsNullOrWhiteSpace(repertoireRACache))
            {
                return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
            }

            // Pour RALibretro, le fichier Data/<GameId>.json le plus recent est le signal
            // le plus fiable du jeu courant pendant les transitions de session.
            RenseignementJeuRA? dernierRenseignement =
                LireDernierRenseignementJeuProject64DepuisData(repertoireRACache);

            if (dernierRenseignement is not null)
            {
                if (
                    string.IsNullOrWhiteSpace(dernierRenseignement.TitreJeu)
                    && renseignementConfiguration is not null
                    && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                )
                {
                    dernierRenseignement = dernierRenseignement with
                    {
                        TitreJeu = renseignementConfiguration.TitreJeu,
                    };
                }

                return MemoriserRenseignementRALibretro(dernierRenseignement);
            }

            string cheminJournal = Path.Combine(repertoireRACache, "RALog.txt");

            if (File.Exists(cheminJournal))
            {
                DateTime horodatageJournal = File.GetLastWriteTimeUtc(cheminJournal);

                if (DateTime.UtcNow - horodatageJournal <= TimeSpan.FromMinutes(15))
                {
                    int identifiantJeu = LireDernierIdentifiantJeuDepuisJournalRA(cheminJournal);

                    if (identifiantJeu > 0)
                    {
                        RenseignementJeuRA renseignement =
                            LireRenseignementJeuProject64DepuisFichierData(
                                repertoireRACache,
                                identifiantJeu
                            ) ?? new RenseignementJeuRA(identifiantJeu, string.Empty);

                        if (
                            renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                            && !string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && !TitresSemblables(
                                renseignementConfiguration.TitreJeu,
                                renseignement.TitreJeu
                            )
                        )
                        {
                            return renseignementConfiguration;
                        }

                        if (
                            string.IsNullOrWhiteSpace(renseignement.TitreJeu)
                            && renseignementConfiguration is not null
                            && !string.IsNullOrWhiteSpace(renseignementConfiguration.TitreJeu)
                        )
                        {
                            renseignement = renseignement with
                            {
                                TitreJeu = renseignementConfiguration.TitreJeu,
                            };
                        }

                        return MemoriserRenseignementRALibretro(renseignement);
                    }
                }
            }

            return renseignementConfiguration ?? LireRenseignementJeuRALibretroDepuisCache();
        }
        catch
        {
            return LireRenseignementJeuRALibretroDepuisCache();
        }
    }

    private static RenseignementJeuRA? MemoriserRenseignementRALibretro(
        RenseignementJeuRA renseignement
    )
    {
        lock (VerrouCacheRALibretro)
        {
            _dernierRenseignementRALibretro = renseignement;
            _dernierHorodatageRenseignementRALibretroUtc = DateTime.UtcNow;
            return _dernierRenseignementRALibretro;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisCache()
    {
        lock (VerrouCacheRALibretro)
        {
            if (
                _dernierRenseignementRALibretro is null
                || DateTime.UtcNow - _dernierHorodatageRenseignementRALibretroUtc
                    > TimeSpan.FromSeconds(5)
            )
            {
                return null;
            }

            return _dernierRenseignementRALibretro;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuRALibretroDepuisConfiguration()
    {
        string cheminConfiguration = TrouverCheminConfigurationRALibretro();

        if (string.IsNullOrWhiteSpace(cheminConfiguration) || !File.Exists(cheminConfiguration))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(cheminConfiguration, Encoding.UTF8)
            );

            if (
                !document.RootElement.TryGetProperty("recent", out JsonElement recent)
                || recent.ValueKind != JsonValueKind.Array
                || recent.GetArrayLength() == 0
            )
            {
                return null;
            }

            JsonElement premierRecent = recent[0];

            if (
                !premierRecent.TryGetProperty("path", out JsonElement path)
                || path.ValueKind != JsonValueKind.String
            )
            {
                return null;
            }

            string cheminJeu = path.GetString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cheminJeu))
            {
                return null;
            }

            string titreJeu = NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
            return string.IsNullOrWhiteSpace(titreJeu) ? null : new RenseignementJeuRA(0, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    private static string TrouverRepertoireRACacheProject64()
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

    private static string TrouverRepertoireRACacheRALibretro()
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

    private static string TrouverCheminConfigurationRALibretro()
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

    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueDepuisSourceLocale(
        string nomEmulateur,
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        if (identifiantJeu <= 0)
        {
            return null;
        }

        try
        {
            string cheminJournal = nomEmulateur switch
            {
                "RALibretro" => Path.Combine(TrouverRepertoireRACacheRALibretro(), "RALog.txt"),
                "LunaProject64" => Path.Combine(TrouverRepertoireRACacheProject64(), "RALog.txt"),
                "RetroArch" => TrouverDernierCheminJournalRetroArch(),
                _ => string.Empty,
            };

            if (string.IsNullOrWhiteSpace(cheminJournal) || !File.Exists(cheminJournal))
            {
                return null;
            }

            RenseignementSuccesRA? renseignement = LireDernierSuccesDepuisJournalRA(cheminJournal);

            if (renseignement is null)
            {
                return null;
            }

            GameAchievementV2? succes = succesConnus.FirstOrDefault(item =>
                item.Id == renseignement.IdentifiantSucces
            );

            return new SuccesDebloqueDetecte
            {
                IdentifiantJeu = identifiantJeu,
                TitreJeu = titreJeu?.Trim() ?? string.Empty,
                IdentifiantSucces = renseignement.IdentifiantSucces,
                TitreSucces = succes?.Title?.Trim() ?? renseignement.TitreSucces,
                Points = succes?.Points ?? 0,
                Hardcore = true,
                DateObtention = DateTimeOffset.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                ),
            };
        }
        catch
        {
            return null;
        }
    }

    public static SuccesDebloqueDetecte? LireDernierSuccesDebloqueRALibretro(
        int identifiantJeu,
        string titreJeu,
        IReadOnlyCollection<GameAchievementV2> succesConnus
    )
    {
        return LireDernierSuccesDebloqueDepuisSourceLocale(
            "RALibretro",
            identifiantJeu,
            titreJeu,
            succesConnus
        );
    }

    private static int LireDernierIdentifiantJeuDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = LireToutesLesLignesAvecPartage(cheminJournal);

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondance = JournalGameIdRegex().Match(ligne);

                if (
                    correspondance.Success
                    && int.TryParse(
                        correspondance.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantJeu
                    )
                )
                {
                    return identifiantJeu;
                }
            }
        }
        catch
        {
            // Le journal RA reste une aide locale facultative.
        }

        return 0;
    }

    private static RenseignementSuccesRA? LireDernierSuccesDepuisJournalRA(string cheminJournal)
    {
        try
        {
            List<string> lignes = LireToutesLesLignesAvecPartage(cheminJournal);

            foreach (string ligne in lignes.AsEnumerable().Reverse())
            {
                Match correspondanceAttribue = JournalSuccesAttribueRegex().Match(ligne);

                if (
                    correspondanceAttribue.Success
                    && int.TryParse(
                        correspondanceAttribue.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribue
                    )
                )
                {
                    return new RenseignementSuccesRA(identifiantSuccesAttribue, string.Empty);
                }

                Match correspondanceAttribution = JournalSuccesAttributionRegex().Match(ligne);

                if (
                    correspondanceAttribution.Success
                    && int.TryParse(
                        correspondanceAttribution.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int identifiantSuccesAttribution
                    )
                )
                {
                    return new RenseignementSuccesRA(
                        identifiantSuccesAttribution,
                        correspondanceAttribution.Groups[2].Value.Trim()
                    );
                }
            }
        }
        catch
        {
            // Le journal RA reste une aide locale facultative.
        }

        return null;
    }

    private static RenseignementJeuRA? LireDernierRenseignementJeuProject64DepuisData(
        string repertoireRACache
    )
    {
        try
        {
            string repertoireData = Path.Combine(repertoireRACache, "Data");

            if (!Directory.Exists(repertoireData))
            {
                return null;
            }

            FileInfo? fichierJeuRecent = new DirectoryInfo(repertoireData)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .Where(fichier => FichierDonneesJeuRegex().IsMatch(fichier.Name))
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (fichierJeuRecent is null)
            {
                return null;
            }

            if (DateTime.UtcNow - fichierJeuRecent.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return null;
            }

            string nomBase = Path.GetFileNameWithoutExtension(fichierJeuRecent.Name);

            if (
                !int.TryParse(
                    nomBase,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int identifiantJeu
                )
            )
            {
                return null;
            }

            return LireRenseignementJeuProject64DepuisFichierData(
                repertoireRACache,
                identifiantJeu
            );
        }
        catch
        {
            return null;
        }
    }

    private static RenseignementJeuRA? LireRenseignementJeuProject64DepuisFichierData(
        string repertoireRACache,
        int identifiantJeu
    )
    {
        try
        {
            string cheminDonneesJeu = Path.Combine(
                repertoireRACache,
                "Data",
                $"{identifiantJeu}.json"
            );

            if (!File.Exists(cheminDonneesJeu))
            {
                return null;
            }

            string contenu = string.Join(
                Environment.NewLine,
                LireToutesLesLignesAvecPartage(cheminDonneesJeu)
            );

            if (string.IsNullOrWhiteSpace(contenu))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(contenu);
            string titreJeu = string.Empty;

            if (
                document.RootElement.TryGetProperty("Title", out JsonElement titre)
                && titre.ValueKind == JsonValueKind.String
            )
            {
                titreJeu = titre.GetString()?.Trim() ?? string.Empty;
            }

            return new RenseignementJeuRA(identifiantJeu, titreJeu);
        }
        catch
        {
            return null;
        }
    }

    private static string NettoyerNomFichierJeu(string nomFichier)
    {
        if (string.IsNullOrWhiteSpace(nomFichier))
        {
            return string.Empty;
        }

        string resultat = nomFichier.Trim();
        resultat = resultat.Replace('_', ' ');
        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    private static string ExtraireTitreDuckStationDepuisMemcardRecente()
    {
        try
        {
            string repertoireDuckStation = TrouverRepertoireDuckStation();

            if (string.IsNullOrWhiteSpace(repertoireDuckStation))
            {
                return string.Empty;
            }

            string repertoireMemcards = Path.Combine(repertoireDuckStation, "memcards");

            if (!Directory.Exists(repertoireMemcards))
            {
                return string.Empty;
            }

            FileInfo? memcardRecente = new DirectoryInfo(repertoireMemcards)
                .EnumerateFiles("*.mcd", SearchOption.TopDirectoryOnly)
                .OrderByDescending(fichier => fichier.LastWriteTimeUtc)
                .FirstOrDefault();

            if (memcardRecente is null)
            {
                return string.Empty;
            }

            if (DateTime.UtcNow - memcardRecente.LastWriteTimeUtc > TimeSpan.FromMinutes(15))
            {
                return string.Empty;
            }

            string nomBase = Path.GetFileNameWithoutExtension(memcardRecente.Name);
            nomBase = SuffixeSlotCarteMemoireRegex().Replace(nomBase, string.Empty);

            if (SerialJeuPlayStationRegex().IsMatch(nomBase))
            {
                string cheminJeu = ResoudreCheminJeuDuckStationDepuisSerial(
                    repertoireDuckStation,
                    nomBase
                );

                if (!string.IsNullOrWhiteSpace(cheminJeu))
                {
                    return NettoyerNomFichierJeu(Path.GetFileNameWithoutExtension(cheminJeu));
                }
            }

            return NettoyerNomFichierJeu(nomBase);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TrouverRepertoireDuckStation()
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

    private static string ResoudreCheminJeuDuckStationDepuisSerial(
        string repertoireDuckStation,
        string serial
    )
    {
        try
        {
            string cheminGamelist = Path.Combine(repertoireDuckStation, "cache", "gamelist.cache");

            if (!File.Exists(cheminGamelist))
            {
                return string.Empty;
            }

            DateTime horodatage = File.GetLastWriteTimeUtc(cheminGamelist);
            Dictionary<string, string> cache;

            lock (VerrouCacheDuckStation)
            {
                if (
                    !string.Equals(
                        _dernierRepertoireDuckStation,
                        repertoireDuckStation,
                        StringComparison.OrdinalIgnoreCase
                    )
                    || _dernierHorodatageCacheGamelistUtc != horodatage
                    || _cacheSerialVersCheminDuckStation.Count == 0
                )
                {
                    _cacheSerialVersCheminDuckStation = ConstruireCacheSerialDuckStation(
                        cheminGamelist
                    );
                    _dernierRepertoireDuckStation = repertoireDuckStation;
                    _dernierHorodatageCacheGamelistUtc = horodatage;
                }

                cache = _cacheSerialVersCheminDuckStation;
            }

            return cache.TryGetValue(serial, out string? cheminJeu) ? cheminJeu : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Dictionary<string, string> ConstruireCacheSerialDuckStation(
        string cheminGamelist
    )
    {
        string[] chaines = ExtraireChainesLisiblesDepuisBinaire(cheminGamelist, 4);
        Dictionary<string, string> correspondances = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < chaines.Length - 1; i++)
        {
            string chaine = chaines[i].Trim();
            string suivante = chaines[i + 1].Trim();

            if (!EstCheminJeuDuckStation(chaine) || !EstSerialJeuPlayStation(suivante))
            {
                continue;
            }

            correspondances[suivante] = chaine;
        }

        return correspondances;
    }

    private static string[] ExtraireChainesLisiblesDepuisBinaire(
        string cheminFichier,
        int longueurMin
    )
    {
        byte[] bytes = File.ReadAllBytes(cheminFichier);
        List<string> chaines = [];
        StringBuilder constructeur = new();

        foreach (byte valeur in bytes)
        {
            if ((valeur >= 32 && valeur <= 126) || valeur == 9)
            {
                constructeur.Append((char)valeur);
            }
            else
            {
                if (constructeur.Length >= longueurMin)
                {
                    chaines.Add(constructeur.ToString());
                }

                constructeur.Clear();
            }
        }

        if (constructeur.Length >= longueurMin)
        {
            chaines.Add(constructeur.ToString());
        }

        return [.. chaines];
    }

    private static bool EstCheminJeuDuckStation(string valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
        {
            return false;
        }

        string extension = Path.GetExtension(valeur);
        string[] extensionsJeuPossibles =
        [
            ".cue",
            ".chd",
            ".iso",
            ".bin",
            ".img",
            ".pbp",
            ".m3u",
            ".ecm",
            ".exe",
        ];

        return extensionsJeuPossibles.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool EstSerialJeuPlayStation(string valeur)
    {
        return !string.IsNullOrWhiteSpace(valeur) && SerialJeuPlayStationRegex().IsMatch(valeur);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspacesMultiplesRegex();

    [GeneratedRegex(@"^v?\d+(\.\d+)+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BlocVersionProject64Regex();

    [GeneratedRegex(@"^\d+(\.\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ValeurNumeriqueSeuleRegex();

    [GeneratedRegex(@"[A-Za-z].*[A-Za-z]", RegexOptions.CultureInvariant)]
    private static partial Regex ContientDeuxLettresRegex();

    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex TexteEntreParenthesesRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex CaracteresNonAlphaNumeriquesRegex();

    [GeneratedRegex("\"([^\"]+)\"|([^\\s]+)", RegexOptions.CultureInvariant)]
    private static partial Regex JetonsLigneCommandeRegex();

    [GeneratedRegex(
        @"(?:Identified game:\s*|Loading game\s+|Starting (?:new )?session for game\s+)(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalGameIdRegex();

    [GeneratedRegex(
        @"Awarding achievement\s+(\d+)\s*:\s*(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesAttributionRegex();

    [GeneratedRegex(
        @"Achievement\s+(\d+)\s+awarded\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex JournalSuccesAttribueRegex();

    [GeneratedRegex(@"^\d+\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex FichierDonneesJeuRegex();

    [GeneratedRegex(
        @"(?:^|[_\-\s])(?:slot|card)\s*\d+$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SuffixeSlotCarteMemoireRegex();

    [GeneratedRegex(
        @"^(?:S[CLN][A-Z]{2}|PBPX)[-_ ]?\d{3,5}(?:[-_ ]?\d{2,3})?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SerialJeuPlayStationRegex();

    [GeneratedRegex(
        @"^[A-Z]{4}\d{5}\s*:\s*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex PrefixeSerialPpssppRegex();

    [GeneratedRegex(
        @"\s*\(([A-Z0-9]{4,8})\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex SuffixeCodeJeuDolphinRegex();

    [GeneratedRegex(
        @"\[RCHEEVOS\]\s+Identified game:\s*(\d+)\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchGameIdentifieRegex();

    [GeneratedRegex(
        @"\[RCHEEVOS\]\s+Starting session for game\s+(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    )]
    private static partial Regex RetroArchSessionJeuRegex();

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("user32.dll")]
    private static partial bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength
    );

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UnicodeString
    {
        public readonly ushort Length;
        public readonly ushort MaximumLength;
        public readonly IntPtr Buffer;
    }
}
