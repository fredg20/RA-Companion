using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Catalogue;

/*
 * Met en cache localement un catalogue minimal des jeux et succès déjà vus
 * afin de fournir un repli hors ligne et des correspondances locales.
 */
namespace RA.Compagnon.Services;

/*
 * Gère la lecture, la mise à jour et la normalisation du catalogue local
 * des jeux observés par l'application.
 */
public sealed partial class ServiceCatalogueJeuxLocal
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const string ExtensionTemporaire = ".tmp";
    private readonly SemaphoreSlim _verrou = new(1, 1);
    private CatalogueJeuxLocal? _catalogue;

    /*
     * Retourne la liste des jeux du catalogue, éventuellement filtrée par
     * identifiants de console.
     */
    public async Task<IReadOnlyList<JeuCatalogueLocal>> ObtenirJeuxAsync(
        IEnumerable<int>? identifiantsConsole = null,
        CancellationToken jetonAnnulation = default
    )
    {
        CatalogueJeuxLocal catalogue = await ChargerCatalogueAsync(jetonAnnulation);

        if (identifiantsConsole is null)
        {
            return catalogue.Jeux;
        }

        HashSet<int> consoles = [.. identifiantsConsole.Where(id => id > 0)];

        if (consoles.Count == 0)
        {
            return catalogue.Jeux;
        }

        return [.. catalogue.Jeux.Where(jeu => consoles.Contains(jeu.ConsoleId))];
    }

    /*
     * Retourne un jeu précis du catalogue local à partir de son identifiant.
     */
    public async Task<JeuCatalogueLocal?> ObtenirJeuAsync(
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        if (identifiantJeu <= 0)
        {
            return null;
        }

        CatalogueJeuxLocal catalogue = await ChargerCatalogueAsync(jetonAnnulation);
        return catalogue.Jeux.FirstOrDefault(jeu => jeu.GameId == identifiantJeu);
    }

    /*
     * Insère ou met à jour un jeu dans le catalogue local à partir des
     * données API et d'un éventuel titre observé localement.
     */
    public async Task EnregistrerJeuAsync(
        GameInfoAndUserProgressV2 jeu,
        string? titreObserveLocal = null,
        CancellationToken jetonAnnulation = default
    )
    {
        if (jeu.Id <= 0)
        {
            return;
        }

        await _verrou.WaitAsync(jetonAnnulation);

        try
        {
            CatalogueJeuxLocal catalogue = await ChargerCatalogueInterneAsync(jetonAnnulation);
            DateTimeOffset maintenant = DateTimeOffset.UtcNow;
            JeuCatalogueLocal? existant = catalogue.Jeux.FirstOrDefault(item =>
                item.GameId == jeu.Id
            );

            if (existant is null)
            {
                existant = new JeuCatalogueLocal { GameId = jeu.Id };
                catalogue.Jeux.Add(existant);
            }

            existant.Titre = (jeu.Title ?? string.Empty).Trim();
            existant.TitreNormalise = NormaliserTitre(existant.Titre);
            existant.ConsoleId = jeu.ConsoleId;
            existant.NomConsole = (jeu.ConsoleName ?? string.Empty).Trim();
            existant.ImageBoxArt = (jeu.ImageBoxArt ?? string.Empty).Trim();
            existant.ImageTitre = (jeu.ImageTitle ?? string.Empty).Trim();
            existant.ImageEnJeu = (jeu.ImageIngame ?? string.Empty).Trim();
            existant.DateMajUtc = maintenant;
            List<SuccesCatalogueLocal> succesConvertis =
            [
                .. jeu.Succes.Values.Select(ConvertirSucces),
            ];
            existant.Succes = FusionnerSuccesSansRegresser(existant.Succes, succesConvertis);

            HashSet<string> titresAlternatifs = new(StringComparer.OrdinalIgnoreCase);

            foreach (string titre in existant.TitresAlternatifs)
            {
                AjouterTitreAlternatif(titresAlternatifs, titre, existant.Titre);
            }

            AjouterTitreAlternatif(titresAlternatifs, titreObserveLocal, existant.Titre);
            AjouterTitreAlternatif(
                titresAlternatifs,
                EnleverParentheses(titreObserveLocal),
                existant.Titre
            );
            AjouterTitreAlternatif(
                titresAlternatifs,
                EnleverParentheses(existant.Titre),
                existant.Titre
            );
            existant.TitresAlternatifs =
            [
                .. titresAlternatifs.OrderBy(titre => titre, StringComparer.OrdinalIgnoreCase),
            ];

            catalogue.DateMajUtc = maintenant;
            _catalogue = catalogue;
            await SauvegarderCatalogueAsync(catalogue, jetonAnnulation);
        }
        finally
        {
            _verrou.Release();
        }
    }

    /*
     * Charge le catalogue en sérialisant l'accès concurrent pour garantir
     * une vision cohérente en mémoire.
     */
    private async Task<CatalogueJeuxLocal> ChargerCatalogueAsync(CancellationToken jetonAnnulation)
    {
        await _verrou.WaitAsync(jetonAnnulation);

        try
        {
            return await ChargerCatalogueInterneAsync(jetonAnnulation);
        }
        finally
        {
            _verrou.Release();
        }
    }

    /*
     * Charge le catalogue depuis la mémoire ou le disque sans reprendre
     * le verrou lorsqu'il est déjà détenu par l'appelant.
     */
    private async Task<CatalogueJeuxLocal> ChargerCatalogueInterneAsync(
        CancellationToken jetonAnnulation
    )
    {
        if (_catalogue is not null)
        {
            return _catalogue;
        }

        string chemin = ObtenirCheminCatalogue();
        string cheminLecture = File.Exists(chemin) ? chemin : chemin + ExtensionTemporaire;

        if (!File.Exists(cheminLecture))
        {
            _catalogue = new CatalogueJeuxLocal();
            return _catalogue;
        }

        try
        {
            await using FileStream flux = File.OpenRead(cheminLecture);
            CatalogueJeuxLocal? catalogue =
                await JsonSerializer.DeserializeAsync<CatalogueJeuxLocal>(
                    flux,
                    OptionsJson,
                    jetonAnnulation
                );
            _catalogue = catalogue ?? new CatalogueJeuxLocal();
        }
        catch (JsonException)
        {
            _catalogue = new CatalogueJeuxLocal();
        }

        return _catalogue;
    }

    /*
     * Sauvegarde le catalogue local via un fichier temporaire pour réduire
     * les risques de corruption.
     */
    private static async Task SauvegarderCatalogueAsync(
        CatalogueJeuxLocal catalogue,
        CancellationToken jetonAnnulation
    )
    {
        string chemin = ObtenirCheminCatalogue();
        string cheminTemporaire = chemin + ExtensionTemporaire;
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);

        await using (FileStream flux = File.Create(cheminTemporaire))
        {
            await JsonSerializer.SerializeAsync(flux, catalogue, OptionsJson, jetonAnnulation);
            await flux.FlushAsync(jetonAnnulation);
        }

        if (File.Exists(chemin))
        {
            File.Delete(chemin);
        }

        File.Move(cheminTemporaire, chemin);
    }

    /*
     * Retourne le chemin du fichier de catalogue local des jeux.
     */
    private static string ObtenirCheminCatalogue()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RA-Compagnon",
            "cache",
            "catalogue-jeux.json"
        );
    }

    /*
     * Convertit un succès API en succès sérialisable dans le catalogue local.
     */
    private static SuccesCatalogueLocal ConvertirSucces(GameAchievementV2 succes)
    {
        return new SuccesCatalogueLocal
        {
            AchievementId = succes.Id,
            Titre = (succes.Title ?? string.Empty).Trim(),
            Description = (succes.Description ?? string.Empty).Trim(),
            Points = succes.Points,
            BadgeName = (succes.BadgeName ?? string.Empty).Trim(),
            Type = (succes.Type ?? string.Empty).Trim(),
            DateMajUtc = DateTimeOffset.UtcNow,
        };
    }

    /*
     * Fusionne les succès reçus avec ceux déjà connus sans perdre les entrées
     * existantes lorsqu'aucune mise à jour n'est fournie.
     */
    private static List<SuccesCatalogueLocal> FusionnerSuccesSansRegresser(
        List<SuccesCatalogueLocal> succesExistants,
        List<SuccesCatalogueLocal> succesRecus
    )
    {
        if (succesExistants.Count == 0)
        {
            return succesRecus;
        }

        if (succesRecus.Count == 0)
        {
            return succesExistants;
        }

        Dictionary<int, SuccesCatalogueLocal> succesFusionnes = succesExistants.ToDictionary(item =>
            item.AchievementId
        );

        foreach (SuccesCatalogueLocal succesRecu in succesRecus)
        {
            succesFusionnes[succesRecu.AchievementId] = succesRecu;
        }

        return [.. succesFusionnes.Values.OrderBy(item => item.AchievementId)];
    }

    /*
     * Ajoute un titre alternatif pertinent au jeu sans dupliquer le titre
     * principal ni les entrées vides.
     */
    private static void AjouterTitreAlternatif(
        HashSet<string> titresAlternatifs,
        string? titre,
        string titrePrincipal
    )
    {
        string titreNettoye = (titre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(titreNettoye))
        {
            return;
        }

        if (string.Equals(titreNettoye, titrePrincipal, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        titresAlternatifs.Add(titreNettoye);
    }

    /*
     * Supprime les parenthèses et leur contenu d'un titre observé localement.
     */
    private static string EnleverParentheses(string? titre)
    {
        return ParenthesesRegex().Replace(titre ?? string.Empty, string.Empty).Trim();
    }

    /*
     * Normalise un titre de jeu pour faciliter les comparaisons locales.
     */
    private static string NormaliserTitre(string? valeur)
    {
        string titre = (valeur ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(titre))
        {
            return string.Empty;
        }

        string sansAccents = string.Concat(
            titre
                .Normalize(NormalizationForm.FormD)
                .Where(caractere =>
                    CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark
                )
        );
        string alphanumerique = CaracteresNonAlphaNumeriquesRegex().Replace(sansAccents, " ");
        return EspacesMultiplesRegex().Replace(alphanumerique, " ").Trim();
    }

    /*
     * Déclare l'expression régulière utilisée pour retirer les parenthèses
     * d'un titre de jeu.
     */
    [GeneratedRegex(@"\s*\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesesRegex();

    /*
     * Déclare l'expression régulière qui élimine les caractères non
     * alphanumériques lors de la normalisation d'un titre.
     */
    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex CaracteresNonAlphaNumeriquesRegex();

    /*
     * Déclare l'expression régulière qui réduit les espaces multiples.
     */
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EspacesMultiplesRegex();
}
