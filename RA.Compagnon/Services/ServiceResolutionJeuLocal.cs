using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Résout un titre local détecté vers un identifiant de jeu RetroAchievements avec prudence.
/// </summary>
public sealed partial class ServiceResolutionJeuLocal
{
    private const double SeuilConfianceJeuxRecents = 0.84;
    private const double SeuilConfianceCatalogue = 0.92;
    private static readonly string CheminJournalResolutionLocale = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RA-Compagnon",
        "journal-resolution-locale.log"
    );

    public static void ReinitialiserJournalSession()
    {
        try
        {
            string? repertoire = Path.GetDirectoryName(CheminJournalResolutionLocale);

            if (!string.IsNullOrWhiteSpace(repertoire))
            {
                Directory.CreateDirectory(repertoire);
            }

            File.WriteAllText(
                CheminJournalResolutionLocale,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] nouvelle_session{Environment.NewLine}"
            );
        }
        catch
        {
            // Cette journalisation reste strictement auxiliaire.
        }
    }

    public static void JournaliserEvenementInterface(string evenement, string details)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalResolutionLocale)!);
            File.AppendAllText(
                CheminJournalResolutionLocale,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={NettoyerPourJournal(evenement)};details={NettoyerPourJournal(details)}{Environment.NewLine}"
                )
            );
        }
        catch
        {
            // Cette journalisation reste strictement auxiliaire.
        }
    }

    public static JeuLocalResolut? ResoudreDepuisJeuxRecents(
        string titreJeuLocal,
        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents
    )
    {
        JeuLocalResolut? resolution = TrouverDansJeuxRecents(titreJeuLocal, jeuxRecents);
        JeuLocalResolut? resolutionRetenue =
            resolution?.ScoreConfiance >= SeuilConfianceJeuxRecents ? resolution : null;
        JournaliserResolution(
            mode: "rapide",
            titreJeuLocal: titreJeuLocal,
            nombreJeuxRecents: jeuxRecents.Count,
            nombreConsolesCandidates: 0,
            meilleureResolutionJeuxRecents: resolution,
            meilleureResolutionCatalogue: null,
            resolutionRetenue: resolutionRetenue
        );
        return resolutionRetenue;
    }

    public static JeuLocalResolut? ResoudreDepuisCatalogueLocal(
        string titreJeuLocal,
        IReadOnlyList<JeuCatalogueLocal> jeuxCatalogueLocal,
        IEnumerable<int> identifiantsConsoleCandidats
    )
    {
        JeuLocalResolut? resolution = TrouverDansCatalogueLocal(
            titreJeuLocal,
            jeuxCatalogueLocal,
            identifiantsConsoleCandidats
        );
        JeuLocalResolut? resolutionRetenue =
            resolution?.ScoreConfiance >= SeuilConfianceCatalogue ? resolution : null;
        JournaliserResolution(
            mode: "catalogue_local_rapide",
            titreJeuLocal: titreJeuLocal,
            nombreJeuxRecents: 0,
            nombreConsolesCandidates: identifiantsConsoleCandidats.Distinct().Count(id => id > 0),
            meilleureResolutionJeuxRecents: null,
            meilleureResolutionCatalogue: resolution,
            resolutionRetenue: resolutionRetenue
        );
        return resolutionRetenue;
    }

    public static async Task<JeuLocalResolut?> ResoudreAsync(
        string titreJeuLocal,
        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents,
        IEnumerable<int> identifiantsConsoleCandidats,
        IReadOnlyList<JeuCatalogueLocal>? jeuxCatalogueLocal,
        Func<int, CancellationToken, Task<IReadOnlyList<GameListEntryV2>>> chargerJeuxSystemeAsync,
        CancellationToken jetonAnnulation = default
    )
    {
        string titreNormalise = NormaliserTitre(titreJeuLocal);

        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            JournaliserResolution(
                mode: "complet",
                titreJeuLocal: titreJeuLocal,
                nombreJeuxRecents: jeuxRecents.Count,
                nombreConsolesCandidates: identifiantsConsoleCandidats
                    .Distinct()
                    .Count(id => id > 0),
                meilleureResolutionJeuxRecents: null,
                meilleureResolutionCatalogue: null,
                resolutionRetenue: null
            );
            return null;
        }

        JeuLocalResolut? resolutionJeuxRecents = TrouverDansJeuxRecents(titreJeuLocal, jeuxRecents);

        if (resolutionJeuxRecents?.ScoreConfiance >= SeuilConfianceJeuxRecents)
        {
            return resolutionJeuxRecents;
        }

        JeuLocalResolut? meilleureResolutionCatalogue = null;
        JeuLocalResolut? resolutionCatalogueLocal = TrouverDansCatalogueLocal(
            titreJeuLocal,
            jeuxCatalogueLocal ?? [],
            identifiantsConsoleCandidats
        );

        if (resolutionCatalogueLocal is not null)
        {
            meilleureResolutionCatalogue = resolutionCatalogueLocal;
        }

        foreach (
            int identifiantConsole in identifiantsConsoleCandidats.Distinct().Where(id => id > 0)
        )
        {
            IReadOnlyList<GameListEntryV2> jeuxSysteme = await chargerJeuxSystemeAsync(
                identifiantConsole,
                jetonAnnulation
            );

            JeuLocalResolut? resolutionCatalogue = TrouverDansCatalogueSysteme(
                titreJeuLocal,
                jeuxSysteme
            );

            if (resolutionCatalogue is null)
            {
                continue;
            }

            if (
                meilleureResolutionCatalogue is null
                || resolutionCatalogue.ScoreConfiance > meilleureResolutionCatalogue.ScoreConfiance
            )
            {
                meilleureResolutionCatalogue = resolutionCatalogue;
            }
        }

        JeuLocalResolut? resolutionRetenue =
            meilleureResolutionCatalogue?.ScoreConfiance >= SeuilConfianceCatalogue
                ? meilleureResolutionCatalogue
            : resolutionJeuxRecents?.ScoreConfiance >= SeuilConfianceCatalogue
                ? resolutionJeuxRecents
            : meilleureResolutionCatalogue?.ScoreConfiance >= SeuilConfianceCatalogue
                ? meilleureResolutionCatalogue
            : null;

        JournaliserResolution(
            mode: "complet",
            titreJeuLocal: titreJeuLocal,
            nombreJeuxRecents: jeuxRecents.Count,
            nombreConsolesCandidates: identifiantsConsoleCandidats.Distinct().Count(id => id > 0),
            meilleureResolutionJeuxRecents: resolutionJeuxRecents,
            meilleureResolutionCatalogue: meilleureResolutionCatalogue,
            resolutionRetenue: resolutionRetenue
        );

        return resolutionRetenue;
    }

    private static JeuLocalResolut? TrouverDansJeuxRecents(
        string titreJeuLocal,
        IReadOnlyList<RecentlyPlayedGameV2> jeuxRecents
    )
    {
        JeuLocalResolut? meilleureResolution = null;

        foreach (RecentlyPlayedGameV2 jeu in jeuxRecents)
        {
            double score = CalculerScoreTitre(titreJeuLocal, jeu.Titre ?? string.Empty);

            if (score <= 0)
            {
                continue;
            }

            JeuLocalResolut resolution = new()
            {
                IdentifiantJeu = jeu.IdentifiantJeu,
                IdentifiantConsole = jeu.IdentifiantConsole,
                TitreLocal = titreJeuLocal.Trim(),
                TitreRetroAchievements = jeu.Titre?.Trim() ?? string.Empty,
                Source = "jeux_recents",
                ScoreConfiance = score,
            };

            if (
                meilleureResolution is null
                || resolution.ScoreConfiance > meilleureResolution.ScoreConfiance
            )
            {
                meilleureResolution = resolution;
            }
        }

        return meilleureResolution;
    }

    private static JeuLocalResolut? TrouverDansCatalogueLocal(
        string titreJeuLocal,
        IReadOnlyList<JeuCatalogueLocal> jeuxCatalogueLocal,
        IEnumerable<int> identifiantsConsoleCandidats
    )
    {
        HashSet<int> consolesCandidates = [.. identifiantsConsoleCandidats.Where(id => id > 0)];
        JeuLocalResolut? meilleureResolution = null;

        foreach (JeuCatalogueLocal jeu in jeuxCatalogueLocal)
        {
            if (consolesCandidates.Count > 0 && !consolesCandidates.Contains(jeu.ConsoleId))
            {
                continue;
            }

            double score = CalculerScoreTitre(titreJeuLocal, jeu.Titre);

            foreach (string titreAlternatif in jeu.TitresAlternatifs)
            {
                score = Math.Max(score, CalculerScoreTitre(titreJeuLocal, titreAlternatif));
            }

            if (score <= 0)
            {
                continue;
            }

            JeuLocalResolut resolution = new()
            {
                IdentifiantJeu = jeu.GameId,
                IdentifiantConsole = jeu.ConsoleId,
                TitreLocal = titreJeuLocal.Trim(),
                TitreRetroAchievements = jeu.Titre.Trim(),
                Source = "catalogue_local",
                ScoreConfiance = score,
            };

            if (
                meilleureResolution is null
                || resolution.ScoreConfiance > meilleureResolution.ScoreConfiance
            )
            {
                meilleureResolution = resolution;
            }
        }

        return meilleureResolution;
    }

    private static JeuLocalResolut? TrouverDansCatalogueSysteme(
        string titreJeuLocal,
        IReadOnlyList<GameListEntryV2> jeuxSysteme
    )
    {
        JeuLocalResolut? meilleureResolution = null;

        foreach (GameListEntryV2 jeu in jeuxSysteme)
        {
            double score = CalculerScoreTitre(titreJeuLocal, jeu.Title ?? string.Empty);

            if (score <= 0)
            {
                continue;
            }

            JeuLocalResolut resolution = new()
            {
                IdentifiantJeu = jeu.Id,
                IdentifiantConsole = jeu.ConsoleId,
                TitreLocal = titreJeuLocal.Trim(),
                TitreRetroAchievements = jeu.Title?.Trim() ?? string.Empty,
                Source = "catalogue_systeme",
                ScoreConfiance = score,
            };

            if (
                meilleureResolution is null
                || resolution.ScoreConfiance > meilleureResolution.ScoreConfiance
            )
            {
                meilleureResolution = resolution;
            }
        }

        return meilleureResolution;
    }

    private static double CalculerScoreTitre(string titreLocal, string titreCandidat)
    {
        string local = NormaliserTitre(titreLocal);
        string candidat = NormaliserTitre(titreCandidat);

        if (string.IsNullOrWhiteSpace(local) || string.IsNullOrWhiteSpace(candidat))
        {
            return 0;
        }

        if (string.Equals(local, candidat, StringComparison.Ordinal))
        {
            return 1;
        }

        if (
            PeutUtiliserRaccourciContient(candidat)
            && local.Contains(candidat, StringComparison.Ordinal)
        )
        {
            return 0.94 + (0.05 * RatioLongueur(candidat, local));
        }

        if (
            PeutUtiliserRaccourciContient(local)
            && candidat.Contains(local, StringComparison.Ordinal)
        )
        {
            return 0.94 + (0.05 * RatioLongueur(local, candidat));
        }

        string[] jetonsLocaux = local.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] jetonsCandidats = candidat.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        HashSet<string> ensembleLocal = [.. jetonsLocaux];
        HashSet<string> ensembleCandidat = [.. jetonsCandidats];
        int intersection = ensembleLocal.Intersect(ensembleCandidat).Count();
        int union = ensembleLocal.Union(ensembleCandidat).Count();

        if (intersection == 0 || union == 0)
        {
            return 0;
        }

        double jaccard = (double)intersection / union;
        double couvertureLocal = (double)intersection / ensembleLocal.Count;
        double couvertureCandidat = (double)intersection / ensembleCandidat.Count;
        double couverture = Math.Min(couvertureLocal, couvertureCandidat);
        double couverturePetitEnsemble =
            (double)intersection / Math.Min(ensembleLocal.Count, ensembleCandidat.Count);
        double prefixe = RatioPrefixeCommun(local, candidat);

        if (
            couverturePetitEnsemble >= 1
            && intersection >= 3
            && couvertureLocal >= 0.75
            && couvertureCandidat >= 0.75
            && Math.Abs(ensembleLocal.Count - ensembleCandidat.Count) <= 2
            && prefixe >= 0.45
        )
        {
            double scoreAbrege =
                0.9
                + (0.05 * Math.Min(couvertureLocal, couvertureCandidat))
                + (0.03 * prefixe)
                + (0.02 * RatioLongueur(local, candidat));

            return Math.Min(1, scoreAbrege);
        }

        return (jaccard * 0.45) + (couverture * 0.4) + (prefixe * 0.15);
    }

    private static bool PeutUtiliserRaccourciContient(string titreNormalise)
    {
        if (string.IsNullOrWhiteSpace(titreNormalise))
        {
            return false;
        }

        string[] jetons = titreNormalise.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (jetons.Length >= 2)
        {
            return true;
        }

        return titreNormalise.Length >= 4;
    }

    private static string NormaliserTitre(string titre)
    {
        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string resultat = titre.Trim().ToLowerInvariant();
        resultat = resultat.Replace("&", " and ", StringComparison.Ordinal);
        resultat = ContenuEntreCrochetsOuParenthesesRegex().Replace(resultat, " ");
        resultat = SupprimerDiacritiques(resultat);
        resultat = CaracteresNonAlphaNumeriquesRegex().Replace(resultat, " ");
        resultat = ArticlesAnglaisRegex().Replace(resultat, " ");
        resultat = EspacesMultiplesRegex().Replace(resultat, " ").Trim();
        return resultat;
    }

    private static string SupprimerDiacritiques(string valeur)
    {
        string valeurNormalisee = valeur.Normalize(NormalizationForm.FormD);
        StringBuilder resultat = new(valeurNormalisee.Length);

        foreach (char caractere in valeurNormalisee)
        {
            UnicodeCategory categorie = CharUnicodeInfo.GetUnicodeCategory(caractere);

            if (categorie != UnicodeCategory.NonSpacingMark)
            {
                resultat.Append(caractere);
            }
        }

        return resultat.ToString().Normalize(NormalizationForm.FormC);
    }

    private static double RatioLongueur(string texteCourt, string texteLong)
    {
        if (texteCourt.Length == 0 || texteLong.Length == 0)
        {
            return 0;
        }

        int plusCourt = Math.Min(texteCourt.Length, texteLong.Length);
        int plusLong = Math.Max(texteCourt.Length, texteLong.Length);
        return (double)plusCourt / plusLong;
    }

    private static double RatioPrefixeCommun(string premier, string second)
    {
        int maximum = Math.Min(premier.Length, second.Length);
        int longueurCommune = 0;

        while (longueurCommune < maximum && premier[longueurCommune] == second[longueurCommune])
        {
            longueurCommune++;
        }

        return maximum == 0 ? 0 : (double)longueurCommune / maximum;
    }

    private static void JournaliserResolution(
        string mode,
        string titreJeuLocal,
        int nombreJeuxRecents,
        int nombreConsolesCandidates,
        JeuLocalResolut? meilleureResolutionJeuxRecents,
        JeuLocalResolut? meilleureResolutionCatalogue,
        JeuLocalResolut? resolutionRetenue
    )
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CheminJournalResolutionLocale)!);
            File.AppendAllText(
                CheminJournalResolutionLocale,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] mode={mode};titreLocal={NettoyerPourJournal(titreJeuLocal)};jeuxRecents={nombreJeuxRecents};consoles={nombreConsolesCandidates};meilleurRecent={FormatterResolution(meilleureResolutionJeuxRecents)};meilleurCatalogue={FormatterResolution(meilleureResolutionCatalogue)};retenu={FormatterResolution(resolutionRetenue)}{Environment.NewLine}"
                )
            );
        }
        catch
        {
            // Cette journalisation reste strictement auxiliaire.
        }
    }

    private static string FormatterResolution(JeuLocalResolut? resolution)
    {
        if (resolution is null)
        {
            return string.Empty;
        }

        return $"{resolution.IdentifiantJeu},{resolution.IdentifiantConsole},{NettoyerPourJournal(resolution.Source)},{resolution.ScoreConfiance.ToString("0.000", CultureInfo.InvariantCulture)},{NettoyerPourJournal(resolution.TitreRetroAchievements)}";
    }

    private static string NettoyerPourJournal(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : valeur.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    [GeneratedRegex(@"[\(\[].*?[\)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex ContenuEntreCrochetsOuParenthesesRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex CaracteresNonAlphaNumeriquesRegex();

    [GeneratedRegex(@"\b(the|a|an)\b", RegexOptions.CultureInvariant)]
    private static partial Regex ArticlesAnglaisRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspacesMultiplesRegex();
}
