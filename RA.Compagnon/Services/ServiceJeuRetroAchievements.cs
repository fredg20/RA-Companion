using System.Collections.Concurrent;
using System.Text.Json;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Catalogue;
using RA.Compagnon.Modeles.Etat;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Compose les données de jeu RetroAchievements utiles à l'affichage principal.
/// </summary>
public sealed class ServiceJeuRetroAchievements
{
    private static readonly TimeSpan DureeCacheDonneesRapides = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DureeCacheDonneesEnrichies = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions OptionsClone = new();
    private readonly ConcurrentDictionary<
        string,
        EntreeCacheJeu<DonneesJeuAffiche>
    > _cacheJeuxRapides = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<
        string,
        EntreeCacheJeu<DonneesJeuAffiche>
    > _cacheJeuxEnrichis = new(StringComparer.Ordinal);

    public async Task<DonneesJeuAffiche> ObtenirDonneesJeuRapidesAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        string cleCache = ConstruireCleCache(pseudo, identifiantJeu);

        if (
            TenterObtenirDepuisCache(
                _cacheJeuxRapides,
                cleCache,
                DureeCacheDonneesRapides,
                out DonneesJeuAffiche? donneesCachees
            ) && donneesCachees is not null
        )
        {
            return donneesCachees;
        }

        GameInfoAndUserProgressV2 jeu =
            await ClientRetroAchievements.ObtenirJeuEtProgressionUtilisateurAsync(
                pseudo,
                cleApiWeb,
                identifiantJeu,
                jetonAnnulation
            );

        DonneesJeuAffiche donnees = new() { Jeu = jeu };
        _cacheJeuxRapides[cleCache] = new EntreeCacheJeu<DonneesJeuAffiche>(Cloner(donnees));
        return donnees;
    }

    public async Task<DonneesJeuAffiche> EnrichirDonneesJeuAsync(
        string pseudo,
        string cleApiWeb,
        DonneesJeuAffiche donneesJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        GameInfoAndUserProgressV2 jeu = donneesJeu.Jeu;
        string cleCache = ConstruireCleCache(pseudo, jeu.Id);

        if (
            TenterObtenirDepuisCache(
                _cacheJeuxEnrichis,
                cleCache,
                DureeCacheDonneesEnrichies,
                out DonneesJeuAffiche? donneesCachees
            ) && donneesCachees is not null
        )
        {
            donneesCachees.Communaute = donneesJeu.Communaute;
            donneesCachees.CommunauteAffichee = donneesJeu.CommunauteAffichee;
            return donneesCachees;
        }

        Task<GameExtendedDetailsV2?> detailsTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirDetailsEtendusJeuAsync(
                cleApiWeb,
                jeu.Id,
                jetonAnnulation
            )
        );
        Task<GameProgressionV2?> progressionTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirProgressionJeuAsync(
                cleApiWeb,
                jeu.Id,
                false,
                jetonAnnulation
            )
        );
        Task<IReadOnlyList<GameRankAndScoreEntryV2>?> rangsTask = TenterAsync(() =>
            ClientRetroAchievements.ObtenirRangEtScoreJeuAsync(
                pseudo,
                cleApiWeb,
                jeu.Id,
                jetonAnnulation
            )
        );

        await Task.WhenAll(detailsTask, progressionTask, rangsTask);

        GameExtendedDetailsV2? details = await detailsTask;
        GameProgressionV2? progression = await progressionTask;
        IReadOnlyList<GameRankAndScoreEntryV2> rangsEtScores = await rangsTask ?? [];

        HydraterJeu(jeu, details);

        DonneesJeuAffiche resultat = new()
        {
            Jeu = jeu,
            DetailsEtendus = details,
            Progression = progression,
            RangsEtScores = rangsEtScores,
            Communaute = donneesJeu.Communaute,
            CommunauteAffichee = donneesJeu.CommunauteAffichee,
        };
        _cacheJeuxEnrichis[cleCache] = new EntreeCacheJeu<DonneesJeuAffiche>(Cloner(resultat));
        return resultat;
    }

    private static void HydraterJeu(GameInfoAndUserProgressV2 jeu, GameExtendedDetailsV2? details)
    {
        if (details is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(jeu.Title) && !string.IsNullOrWhiteSpace(details.Title))
        {
            jeu.Title = details.Title.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ConsoleName)
            && !string.IsNullOrWhiteSpace(details.ConsoleName)
        )
        {
            jeu.ConsoleName = details.ConsoleName.Trim();
        }

        if (jeu.ConsoleId <= 0 && details.ConsoleId > 0)
        {
            jeu.ConsoleId = details.ConsoleId;
        }

        if (string.IsNullOrWhiteSpace(jeu.Released) && !string.IsNullOrWhiteSpace(details.Released))
        {
            jeu.Released = details.Released.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.Developer)
            && !string.IsNullOrWhiteSpace(details.Developer)
        )
        {
            jeu.Developer = details.Developer.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.Publisher)
            && !string.IsNullOrWhiteSpace(details.Publisher)
        )
        {
            jeu.Publisher = details.Publisher.Trim();
        }

        if (string.IsNullOrWhiteSpace(jeu.Genre) && !string.IsNullOrWhiteSpace(details.Genre))
        {
            jeu.Genre = details.Genre.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageBoxArt)
            && !string.IsNullOrWhiteSpace(details.ImageBoxArt)
        )
        {
            jeu.ImageBoxArt = details.ImageBoxArt.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageTitle)
            && !string.IsNullOrWhiteSpace(details.ImageTitle)
        )
        {
            jeu.ImageTitle = details.ImageTitle.Trim();
        }

        if (
            string.IsNullOrWhiteSpace(jeu.ImageIngame)
            && !string.IsNullOrWhiteSpace(details.ImageIngame)
        )
        {
            jeu.ImageIngame = details.ImageIngame.Trim();
        }

        if (jeu.NumDistinctPlayers <= 0 && details.NumDistinctPlayers > 0)
        {
            jeu.NumDistinctPlayers = details.NumDistinctPlayers;
        }

        if (
            (jeu.Achievements is null || jeu.Achievements.Count == 0)
            && details.Achievements.Count > 0
        )
        {
            jeu.Achievements = details.Achievements;
            jeu.NumAchievements = details.NumAchievements;
        }
    }

    private static async Task<T?> TenterAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return default;
        }
    }

    private static bool TenterObtenirDepuisCache(
        ConcurrentDictionary<string, EntreeCacheJeu<DonneesJeuAffiche>> cache,
        string cleCache,
        TimeSpan dureeValidite,
        out DonneesJeuAffiche? donnees
    )
    {
        if (
            cache.TryGetValue(cleCache, out EntreeCacheJeu<DonneesJeuAffiche>? entree)
            && DateTimeOffset.UtcNow - entree.DateMajUtc <= dureeValidite
        )
        {
            donnees = Cloner(entree.Valeur);
            return donnees is not null;
        }

        donnees = null;
        return false;
    }

    private static string ConstruireCleCache(string pseudo, int identifiantJeu)
    {
        return $"{pseudo.Trim()}|{identifiantJeu}";
    }

    private static T Cloner<T>(T source)
    {
        byte[] donnees = JsonSerializer.SerializeToUtf8Bytes(source, OptionsClone);
        return JsonSerializer.Deserialize<T>(donnees, OptionsClone)!;
    }

    private sealed record EntreeCacheJeu<T>(T Valeur)
    {
        public DateTimeOffset DateMajUtc { get; } = DateTimeOffset.UtcNow;
    }

    public static DonneesJeuAffiche? ConstruireDonneesJeuDepuisCacheLocal(
        JeuCatalogueLocal? jeuCatalogue,
        EtatJeuUtilisateurLocal? etatUtilisateur
    )
    {
        if (jeuCatalogue is null || jeuCatalogue.GameId <= 0)
        {
            return null;
        }

        Dictionary<int, EtatSuccesUtilisateurLocal> etatsSucces =
            etatUtilisateur?.Succes.ToDictionary(item => item.AchievementId) ?? [];

        Dictionary<string, GameAchievementV2> succes = [];

        foreach (SuccesCatalogueLocal succesCatalogue in jeuCatalogue.Succes)
        {
            etatsSucces.TryGetValue(
                succesCatalogue.AchievementId,
                out EtatSuccesUtilisateurLocal? etatSucces
            );

            GameAchievementV2 succesJeu = new()
            {
                Id = succesCatalogue.AchievementId,
                Title = succesCatalogue.Titre,
                Description = succesCatalogue.Description,
                Points = succesCatalogue.Points,
                BadgeName = succesCatalogue.BadgeName,
                Type = succesCatalogue.Type,
                DateEarned = etatSucces?.DateDeblocageUtc ?? string.Empty,
                DateEarnedHardcore = etatSucces?.DateDeblocageHardcoreUtc ?? string.Empty,
            };
            succes[succesJeu.Id.ToString()] = succesJeu;
        }

        int nbSucces = succes.Count;
        int nbSuccesDebloques =
            etatUtilisateur?.NbSuccesDebloques
            ?? etatsSucces.Values.Count(item => item.EstDebloque);
        int nbSuccesHardcore =
            etatUtilisateur?.NbSuccesDebloquesHardcore
            ?? etatsSucces.Values.Count(item => item.EstHardcore);
        double progression =
            etatUtilisateur?.ProgressionPourcentage
            ?? (nbSucces <= 0 ? 0 : (double)nbSuccesDebloques / nbSucces * 100d);

        GameInfoAndUserProgressV2 jeu = new()
        {
            Id = jeuCatalogue.GameId,
            Title = jeuCatalogue.Titre,
            ConsoleId = jeuCatalogue.ConsoleId,
            ConsoleName = jeuCatalogue.NomConsole,
            ImageBoxArt = jeuCatalogue.ImageBoxArt,
            ImageTitle = jeuCatalogue.ImageTitre,
            ImageIngame = jeuCatalogue.ImageEnJeu,
            Achievements = succes,
            NumAchievements = nbSucces,
            NumAwardedToUser = nbSuccesDebloques,
            NumAwardedToUserHardcore = nbSuccesHardcore,
            UserCompletion = $"{progression:0.##}%",
        };

        return new DonneesJeuAffiche { Jeu = jeu };
    }
}
