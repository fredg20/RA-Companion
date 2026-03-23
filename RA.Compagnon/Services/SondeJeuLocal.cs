using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte localement un émulateur actif et tente d'estimer le jeu chargé.
/// </summary>
public sealed class SondeJeuLocal
{
    private static readonly TimeSpan DureeCacheLigneCommande = TimeSpan.FromSeconds(3);
    private readonly Dictionary<int, LigneCommandeCachee> _cacheLignesCommande = [];

    private static readonly string[] ExtensionsJeuConnues =
    [
        ".zip",
        ".7z",
        ".rar",
        ".cue",
        ".iso",
        ".bin",
        ".img",
        ".chd",
        ".cso",
        ".rvz",
        ".gdi",
        ".wbfs",
        ".nes",
        ".fds",
        ".sfc",
        ".smc",
        ".gb",
        ".gbc",
        ".gba",
        ".nds",
        ".n64",
        ".z64",
        ".v64",
        ".gen",
        ".md",
        ".gg",
        ".sms",
        ".pce",
        ".cue",
        ".pbp",
    ];

    private static readonly DefinitionEmulateur[] EmulateursPrisEnCharge =
    [
        new("Luna's Project64", ["project64"], [" - Project64", " | Project64"]),
        new("RALibRetro", ["ralibretro"], [" - RALibRetro", " | RALibRetro"]),
        new("BizHawk", ["emuhawk"], [" - BizHawk", " | BizHawk"]),
        new("Flycast", ["flycast"], [" - Flycast", " | Flycast"]),
        new("Dolphin", ["dolphin"], [" | Dolphin", " - Dolphin"]),
        new("PPSSPP", ["ppssppwindows", "ppssppwindows64"], [" - PPSSPP", " | PPSSPP"]),
        new("DuckStation", ["duckstation"], [" | DuckStation", " - DuckStation"]),
        new("PCSX2", ["pcsx2", "pcsx2-qt"], [" | PCSX2", " - PCSX2"]),
        new("RetroArch", ["retroarch"], [" - RetroArch", " | RetroArch"]),
    ];

    /// <summary>
    /// Retourne le premier jeu localement détecté parmi les émulateurs connus.
    /// </summary>
    public JeuDetecteLocalement? DetecterJeu()
    {
        JeuDetecteLocalement? meilleurJeu = null;

        foreach (DefinitionEmulateur emulateur in EmulateursPrisEnCharge)
        {
            foreach (string nomProcessus in emulateur.NomsProcessus)
            {
                foreach (Process processus in Process.GetProcessesByName(nomProcessus))
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(processus.MainWindowTitle))
                        {
                            continue;
                        }

                        string ligneCommande = ObtenirLigneCommande(processus);
                        string cheminJeuLigneCommande = ExtraireCheminJeu(ligneCommande);
                        string cheminJeuEstime = ExtraireCheminJeu(processus.MainWindowTitle);
                        string titreJeuEstime = ExtraireTitreJeu(
                            processus.MainWindowTitle,
                            emulateur.SeparateursTitre,
                            string.IsNullOrWhiteSpace(cheminJeuLigneCommande)
                                ? cheminJeuEstime
                                : cheminJeuLigneCommande
                        );

                        int scoreConfiance = CalculerScoreConfiance(
                            emulateur,
                            processus.ProcessName,
                            processus.MainWindowTitle,
                            titreJeuEstime,
                            cheminJeuEstime,
                            cheminJeuLigneCommande
                        );

                        if (scoreConfiance <= 0)
                        {
                            continue;
                        }

                        JeuDetecteLocalement jeuDetecte = new()
                        {
                            IdentifiantProcessus = processus.Id,
                            NomEmulateur = emulateur.NomAffiche,
                            NomProcessus = processus.ProcessName,
                            TitreFenetre = processus.MainWindowTitle,
                            TitreJeuEstime = titreJeuEstime,
                            CheminJeuEstime = cheminJeuEstime,
                            CheminJeuLigneCommande = cheminJeuLigneCommande,
                            LigneCommande = ligneCommande,
                            CheminExecutable = ObtenirCheminExecutable(processus),
                            ScoreConfiance = scoreConfiance,
                        };

                        if (
                            meilleurJeu is null
                            || jeuDetecte.ScoreConfiance > meilleurJeu.ScoreConfiance
                        )
                        {
                            meilleurJeu = jeuDetecte;
                        }
                    }
                    catch
                    {
                        // Ignore les processus devenus inaccessibles pendant l'énumération.
                    }
                    finally
                    {
                        processus.Dispose();
                    }
                }
            }
        }

        return meilleurJeu;
    }

    /// <summary>
    /// Tente de déduire le titre du jeu à partir du titre de fenêtre de l'émulateur.
    /// </summary>
    private static string ExtraireTitreJeu(
        string titreFenetre,
        IReadOnlyList<string> separateurs,
        string cheminJeuEstime
    )
    {
        if (!string.IsNullOrWhiteSpace(cheminJeuEstime))
        {
            return NettoyerTitreJeu(cheminJeuEstime);
        }

        string titreNettoye = titreFenetre.Trim();

        foreach (string separateur in separateurs)
        {
            int indexSeparateur = titreNettoye.IndexOf(
                separateur,
                StringComparison.OrdinalIgnoreCase
            );

            if (indexSeparateur > 0)
            {
                return NettoyerTitreJeu(titreNettoye[..indexSeparateur]);
            }
        }

        return NettoyerTitreJeu(titreNettoye);
    }

    /// <summary>
    /// Essaie d'extraire un chemin ou un nom de fichier de jeu depuis le titre de fenêtre.
    /// </summary>
    private static string ExtraireCheminJeu(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string motifExtensions = string.Join(
            "|",
            ExtensionsJeuConnues.Select(extension => Regex.Escape(extension.TrimStart('.')))
        );

        Match correspondanceCheminComplet = Regex.Match(
            titreFenetre,
            $@"(?<jeu>[A-Za-z]:\\[^""\r\n|]*?\.({motifExtensions}))",
            RegexOptions.IgnoreCase
        );

        if (correspondanceCheminComplet.Success)
        {
            return correspondanceCheminComplet.Groups["jeu"].Value.Trim();
        }

        Match correspondanceFichier = Regex.Match(
            titreFenetre,
            $@"(?<jeu>[^\\/:*?""<>|\r\n]+\.({motifExtensions}))",
            RegexOptions.IgnoreCase
        );

        if (correspondanceFichier.Success)
        {
            return correspondanceFichier.Groups["jeu"].Value.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Nettoie un titre de jeu estimé pour le rendre plus lisible.
    /// </summary>
    private static string NettoyerTitreJeu(string titreBrut)
    {
        string titreNettoye = titreBrut.Trim();

        if (titreNettoye.Contains('\\') || titreNettoye.Contains('/'))
        {
            titreNettoye = Path.GetFileName(titreNettoye);
        }

        foreach (string extension in ExtensionsJeuConnues)
        {
            if (titreNettoye.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                titreNettoye = titreNettoye[..^extension.Length];
                break;
            }
        }

        titreNettoye = titreNettoye.Replace('_', ' ').Trim();

        while (titreNettoye.Contains("  ", StringComparison.Ordinal))
        {
            titreNettoye = titreNettoye.Replace("  ", " ", StringComparison.Ordinal);
        }

        return titreNettoye.Trim(' ', '-', '|');
    }

    /// <summary>
    /// Récupère le chemin de l'exécutable si Windows nous y autorise.
    /// </summary>
    private static string ObtenirCheminExecutable(Process processus)
    {
        try
        {
            return processus.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Essaie de lire la ligne de commande du processus via PowerShell et CIM.
    /// </summary>
    private string ObtenirLigneCommande(Process processus)
    {
        if (
            _cacheLignesCommande.TryGetValue(
                processus.Id,
                out LigneCommandeCachee? ligneCommandeCachee
            )
            && string.Equals(
                ligneCommandeCachee.TitreFenetre,
                processus.MainWindowTitle,
                StringComparison.Ordinal
            )
            && DateTime.UtcNow - ligneCommandeCachee.DateLectureUtc < DureeCacheLigneCommande
        )
        {
            return ligneCommandeCachee.LigneCommande;
        }

        string cheminPowerShell = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe"
        );

        if (!File.Exists(cheminPowerShell))
        {
            _cacheLignesCommande[processus.Id] = new LigneCommandeCachee(
                processus.MainWindowTitle,
                string.Empty
            );
            return string.Empty;
        }

        try
        {
            using Process processusPowerShell = new();
            processusPowerShell.StartInfo = new ProcessStartInfo
            {
                FileName = cheminPowerShell,
                Arguments =
                    $"-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId = {processus.Id}').CommandLine\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            processusPowerShell.Start();
            if (!processusPowerShell.WaitForExit(700))
            {
                try
                {
                    processusPowerShell.Kill();
                }
                catch
                {
                    // Ignore les cas où le processus se ferme entre-temps.
                }

                _cacheLignesCommande[processus.Id] = new LigneCommandeCachee(
                    processus.MainWindowTitle,
                    string.Empty
                );
                return string.Empty;
            }

            string ligneCommande = processusPowerShell.StandardOutput.ReadToEnd().Trim();

            _cacheLignesCommande[processus.Id] = new LigneCommandeCachee(
                processus.MainWindowTitle,
                ligneCommande
            );
            return ligneCommande;
        }
        catch
        {
            _cacheLignesCommande[processus.Id] = new LigneCommandeCachee(
                processus.MainWindowTitle,
                string.Empty
            );
            return string.Empty;
        }
    }

    /// <summary>
    /// Évalue rapidement si le titre de fenêtre ressemble à un vrai jeu plutôt qu'au seul nom de l'émulateur.
    /// </summary>
    private static int CalculerScoreConfiance(
        DefinitionEmulateur emulateur,
        string nomProcessus,
        string titreFenetre,
        string titreJeuEstime,
        string cheminJeuEstime,
        string cheminJeuLigneCommande
    )
    {
        string titreFenetreNormalise = titreFenetre.Trim();
        string titreJeuNormalise = titreJeuEstime.Trim();

        if (string.IsNullOrWhiteSpace(titreJeuNormalise))
        {
            return 0;
        }

        int score = 0;

        if (!titreFenetreNormalise.Equals(titreJeuNormalise, StringComparison.OrdinalIgnoreCase))
        {
            score += 2;
        }

        if (
            !titreJeuNormalise.Equals(emulateur.NomAffiche, StringComparison.OrdinalIgnoreCase)
            && !titreJeuNormalise.Equals(nomProcessus, StringComparison.OrdinalIgnoreCase)
        )
        {
            score += 2;
        }

        if (titreJeuNormalise.Length >= 4)
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(cheminJeuEstime))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(cheminJeuLigneCommande))
        {
            score += 4;
        }

        if (
            titreFenetreNormalise.Contains("no disc", StringComparison.OrdinalIgnoreCase)
            || titreFenetreNormalise.Contains("no game", StringComparison.OrdinalIgnoreCase)
            || titreFenetreNormalise.Contains("bios", StringComparison.OrdinalIgnoreCase)
        )
        {
            score = 0;
        }

        return score;
    }

    /// <summary>
    /// Décrit un émulateur reconnu par la sonde locale.
    /// </summary>
    private sealed class DefinitionEmulateur
    {
        public DefinitionEmulateur(
            string nomAffiche,
            IReadOnlyList<string> nomsProcessus,
            IReadOnlyList<string> separateursTitre
        )
        {
            NomAffiche = nomAffiche;
            NomsProcessus = nomsProcessus;
            SeparateursTitre = separateursTitre;
        }

        public string NomAffiche { get; }

        public IReadOnlyList<string> NomsProcessus { get; }

        public IReadOnlyList<string> SeparateursTitre { get; }
    }

    /// <summary>
    /// Mémorise temporairement une ligne de commande lue pour éviter de relancer PowerShell trop souvent.
    /// </summary>
    private sealed class LigneCommandeCachee
    {
        public LigneCommandeCachee(string titreFenetre, string ligneCommande)
        {
            TitreFenetre = titreFenetre;
            LigneCommande = ligneCommande;
            DateLectureUtc = DateTime.UtcNow;
        }

        public string TitreFenetre { get; }

        public string LigneCommande { get; }

        public DateTime DateLectureUtc { get; }
    }
}
