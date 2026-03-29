using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte localement les principaux émulateurs connus à partir des processus et titres de fenêtre.
/// </summary>
public sealed class ServiceSondeLocaleEmulateurs
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private const int ProcessCommandLineInformation = 60;
    private static readonly Lock VerrouCacheDuckStation = new();
    private static string _dernierRepertoireDuckStation = string.Empty;
    private static DateTime _dernierHorodatageCacheGamelistUtc = DateTime.MinValue;
    private static Dictionary<string, string> _cacheSerialVersCheminDuckStation = [];

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
        new(
            "DuckStation",
            ["duckstation", "duckstation-qt", "duckstation-nogui", "duckstation-sdl"],
            ExtraireTitreDuckStation
        ),
        new(
            "PCSX2",
            ["pcsx2", "pcsx2-qt"],
            ExtraireTitrePCSX2
        ),
        new(
            "PPSSPP",
            ["ppsspp", "ppssppwindows", "ppssppwindows64"],
            (_, titre) => ExtraireTitreAvecSeparateurs(titre, "PPSSPP")
        ),
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
        new("Flycast", ["flycast"], (_, titre) => ExtraireTitreAvecSeparateurs(titre, "Flycast")),
        new(
            "Project64",
            ["project64"],
            (_, titre) => ExtraireTitreAvecSeparateurs(titre, "Project64", "Project 64")
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

    public bool SonderPresenceEmulateur()
    {
        try
        {
            Process[] processus = Process.GetProcesses();
            return Definitions.Any(definition =>
                processus.Any(processusCourant => CorrespondNomProcessus(processusCourant, definition))
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
        string informationsDiagnostic = string.Empty;

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
            $"{definition.NomEmulateur}|{processusCible.ProcessName}|{titreFenetre}|{titreJeuProbable}|{informationsDiagnostic}";

        return new EtatSondeLocaleEmulateur
        {
            EmulateurDetecte = true,
            NomEmulateur = definition.NomEmulateur,
            NomProcessus = processusCible.ProcessName,
            TitreFenetre = titreFenetre,
            TitreJeuProbable = titreJeuProbable,
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

        // DuckStation et PCSX2 ont des variantes de fenêtres/outils qui rendent le fallback
        // par titre trop bruyant (navigateurs, installateur, dialogues internes, etc.).
        if (
            string.Equals(definition.NomEmulateur, "DuckStation", StringComparison.Ordinal)
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

        resultat = Regex.Replace(resultat, @"\s+", " ").Trim();
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

        titreNettoye = Regex.Replace(
            titreNettoye,
            @"\s*\(([A-Z0-9]{4,8})\)\s*$",
            string.Empty,
            RegexOptions.IgnoreCase
        ).Trim();

        return titreNettoye;
    }

    private static bool EstDialogueDolphin(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return false;
        }

        string titreNormalise = titre.Trim().ToLowerInvariant();

        return titreNormalise is
            "selectionner un dossier"
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

        return titreNormalise is
            "mise a jour automatique"
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

        if (Regex.IsMatch(valeurMinuscule, @"^\d+(\.\d+)*$"))
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

        if (Regex.IsMatch(valeur, @"[A-Za-z].*[A-Za-z]"))
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
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] detecte={etat.EmulateurDetecte};emulateur={NettoyerPourJournal(etat.NomEmulateur)};processus={NettoyerPourJournal(etat.NomProcessus)};titreFenetre={NettoyerPourJournal(etat.TitreFenetre)};titreJeu={NettoyerPourJournal(etat.TitreJeuProbable)};diagnostic={NettoyerPourJournal(etat.InformationsDiagnostic)};signature={NettoyerPourJournal(etat.Signature)}{Environment.NewLine}"
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

                    GetWindowThreadProcessId(handle, out uint identifiantProcessusFenetre);

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
                AutomationElement fenetre = AutomationElement.FromHandle(processus.MainWindowHandle);
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
            int tailleRetour = 0;
            int statut = NtQueryInformationProcess(
                processus.Handle,
                ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out tailleRetour
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

        MatchCollection correspondances = Regex.Matches(
            ligneCommande,
            "\"([^\"]+)\"|([^\\s]+)",
            RegexOptions.CultureInvariant
        );

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
            string valeur =
                correspondance.Groups[1].Success
                    ? correspondance.Groups[1].Value
                    : correspondance.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(valeur) || valeur.StartsWith("-", StringComparison.Ordinal))
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

    private static string NettoyerNomFichierJeu(string nomFichier)
    {
        if (string.IsNullOrWhiteSpace(nomFichier))
        {
            return string.Empty;
        }

        string resultat = nomFichier.Trim();
        resultat = resultat.Replace('_', ' ');
        resultat = Regex.Replace(resultat, @"\s+", " ").Trim();
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
            nomBase = Regex.Replace(nomBase, @"_[12]$", string.Empty);

            if (Regex.IsMatch(nomBase, @"^[A-Z]{4}-\d{5}$", RegexOptions.CultureInvariant))
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

    private static Dictionary<string, string> ConstruireCacheSerialDuckStation(string cheminGamelist)
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

    private static string[] ExtraireChainesLisiblesDepuisBinaire(string cheminFichier, int longueurMin)
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
        return !string.IsNullOrWhiteSpace(valeur)
            && Regex.IsMatch(valeur, @"^[A-Z]{4}-\d{5}$", RegexOptions.CultureInvariant);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
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
