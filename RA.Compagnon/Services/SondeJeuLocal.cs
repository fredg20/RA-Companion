using System.Diagnostics;
using System.IO;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Détecte localement l'état live de RetroArch sans heuristiques lourdes ni appels externes.
/// </summary>
public sealed class SondeJeuLocal
{
    private static readonly TimeSpan DureeCacheDetection = TimeSpan.FromMilliseconds(200);
    private DateTime _dateDerniereDetectionUtc = DateTime.MinValue;
    private string _signatureDerniereDetection = string.Empty;
    private JeuDetecteLocalement? _dernierJeuDetecte;

    /// <summary>
    /// Retourne l'état local actuel de RetroArch si l'émulateur est lancé.
    /// </summary>
    public JeuDetecteLocalement? DetecterJeu()
    {
        Process? processusRetroArch = null;

        try
        {
            processusRetroArch = Process
                .GetProcessesByName("retroarch")
                .FirstOrDefault(processus => !string.IsNullOrWhiteSpace(processus.MainWindowTitle));

            if (processusRetroArch is null)
            {
                MemoriserDetection(string.Empty, null);
                return null;
            }

            string titreFenetre = processusRetroArch.MainWindowTitle.Trim();
            string signatureDetection = $"{processusRetroArch.Id}|{titreFenetre}".Trim();

            if (
                string.Equals(
                    _signatureDerniereDetection,
                    signatureDetection,
                    StringComparison.Ordinal
                )
                && DateTime.UtcNow - _dateDerniereDetectionUtc < DureeCacheDetection
            )
            {
                return ClonerJeuDetecte(_dernierJeuDetecte);
            }

            JeuDetecteLocalement jeuDetecte = ConstruireDetectionRetroArch(
                processusRetroArch,
                titreFenetre
            );

            MemoriserDetection(signatureDetection, jeuDetecte);
            return ClonerJeuDetecte(jeuDetecte);
        }
        catch
        {
            MemoriserDetection(string.Empty, null);
            return null;
        }
        finally
        {
            processusRetroArch?.Dispose();
        }
    }

    /// <summary>
    /// Construit un état de détection stable pour RetroArch.
    /// </summary>
    private static JeuDetecteLocalement ConstruireDetectionRetroArch(
        Process processusRetroArch,
        string titreFenetre
    )
    {
        string titreJeu = ExtraireTitreJeuRetroArch(titreFenetre);
        string cheminJeu = ExtraireCheminJeuDepuisTitre(titreFenetre);

        return new JeuDetecteLocalement
        {
            IdentifiantProcessus = processusRetroArch.Id,
            NomEmulateur = "RetroArch",
            NomProcessus = processusRetroArch.ProcessName,
            TitreFenetre = titreFenetre,
            TitreJeuEstime = titreJeu,
            CheminJeuEstime = cheminJeu,
            CheminJeuLigneCommande = string.Empty,
            LigneCommande = string.Empty,
            CheminExecutable = ObtenirCheminExecutable(processusRetroArch),
            ScoreConfiance = string.IsNullOrWhiteSpace(titreJeu) ? 3 : 8,
        };
    }

    /// <summary>
    /// Retire l'habillage de fenêtre RetroArch pour ne garder qu'un vrai titre de jeu quand il existe.
    /// </summary>
    private static string ExtraireTitreJeuRetroArch(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string titre = titreFenetre.Trim();

        if (TitreRetroArchEstGenerique(titre))
        {
            return string.Empty;
        }

        string[] separateurs = [" - RetroArch", " | RetroArch"];

        foreach (string separateur in separateurs)
        {
            int indexSeparateur = titre.IndexOf(separateur, StringComparison.OrdinalIgnoreCase);

            if (indexSeparateur > 0)
            {
                return NettoyerTitreJeu(titre[..indexSeparateur]);
            }
        }

        return NettoyerTitreJeu(titre);
    }

    /// <summary>
    /// Essaie d'extraire un chemin ou un nom de fichier de jeu si RetroArch l'affiche dans sa fenêtre.
    /// </summary>
    private static string ExtraireCheminJeuDepuisTitre(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return string.Empty;
        }

        string[] morceaux = titreFenetre.Split(
            ['|', '-'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        foreach (string morceau in morceaux)
        {
            if (RessembleAFichierJeu(morceau))
            {
                return morceau.Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Ignore les états transitoires qui ne représentent pas un jeu chargé.
    /// </summary>
    private static bool TitreRetroArchEstGenerique(string titreFenetre)
    {
        if (string.IsNullOrWhiteSpace(titreFenetre))
        {
            return true;
        }

        string titre = titreFenetre.Trim();

        if (titre.Equals("RetroArch", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (titre.StartsWith("RetroArch ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return titre.Contains("no core", StringComparison.OrdinalIgnoreCase)
            || titre.Contains("no game", StringComparison.OrdinalIgnoreCase)
            || titre.Contains("main menu", StringComparison.OrdinalIgnoreCase)
            || titre.Contains("menu", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nettoie le titre brut du jeu pour l'affichage.
    /// </summary>
    private static string NettoyerTitreJeu(string titreBrut)
    {
        string titre = titreBrut.Trim();

        if (titre.Contains('\\') || titre.Contains('/'))
        {
            titre = Path.GetFileName(titre);
        }

        string[] extensionsConnues =
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
            ".pbp",
        ];

        foreach (string extension in extensionsConnues)
        {
            if (titre.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                titre = titre[..^extension.Length];
                break;
            }
        }

        titre = titre.Replace('_', ' ').Trim();

        while (titre.Contains("  ", StringComparison.Ordinal))
        {
            titre = titre.Replace("  ", " ", StringComparison.Ordinal);
        }

        return titre.Trim(' ', '-', '|');
    }

    /// <summary>
    /// Vérifie rapidement si un fragment de titre ressemble à un nom de fichier de jeu.
    /// </summary>
    private static bool RessembleAFichierJeu(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
        {
            return false;
        }

        string extension = Path.GetExtension(texte.Trim());

        return !string.IsNullOrWhiteSpace(extension)
            && extension.Length <= 5
            && extension.StartsWith('.');
    }

    /// <summary>
    /// Récupère le chemin de l'exécutable si Windows l'autorise.
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
    /// Met à jour le cache court de détection.
    /// </summary>
    private void MemoriserDetection(string signatureDetection, JeuDetecteLocalement? jeuDetecte)
    {
        _signatureDerniereDetection = signatureDetection;
        _dateDerniereDetectionUtc = DateTime.UtcNow;
        _dernierJeuDetecte = ClonerJeuDetecte(jeuDetecte);
    }

    /// <summary>
    /// Retourne une copie pour éviter qu'un appelant modifie l'instance cachée.
    /// </summary>
    private static JeuDetecteLocalement? ClonerJeuDetecte(JeuDetecteLocalement? jeuDetecte)
    {
        if (jeuDetecte is null)
        {
            return null;
        }

        return new JeuDetecteLocalement
        {
            IdentifiantProcessus = jeuDetecte.IdentifiantProcessus,
            NomEmulateur = jeuDetecte.NomEmulateur,
            NomProcessus = jeuDetecte.NomProcessus,
            TitreFenetre = jeuDetecte.TitreFenetre,
            TitreJeuEstime = jeuDetecte.TitreJeuEstime,
            CheminJeuEstime = jeuDetecte.CheminJeuEstime,
            CheminJeuLigneCommande = jeuDetecte.CheminJeuLigneCommande,
            CheminJeuRetenu = jeuDetecte.CheminJeuRetenu,
            EmpreinteLocale = jeuDetecte.EmpreinteLocale,
            IdentifiantJeuRetroAchievements = jeuDetecte.IdentifiantJeuRetroAchievements,
            TitreJeuRetroAchievements = jeuDetecte.TitreJeuRetroAchievements,
            NomConsoleRetroAchievements = jeuDetecte.NomConsoleRetroAchievements,
            LigneCommande = jeuDetecte.LigneCommande,
            CheminExecutable = jeuDetecte.CheminExecutable,
            ScoreConfiance = jeuDetecte.ScoreConfiance,
        };
    }
}
