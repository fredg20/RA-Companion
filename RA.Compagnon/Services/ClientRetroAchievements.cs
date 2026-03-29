using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RA.Compagnon.Modeles.Api.V2.Achievement;
using RA.Compagnon.Modeles.Api.V2.Feed;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Api.V2.System;
using RA.Compagnon.Modeles.Api.V2.User;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Encapsule les appels minimaux à l'API RetroAchievements pour le MVP.
/// </summary>
public sealed class ClientRetroAchievements
{
    private sealed record JeuUtilisateurCache(
        GameInfoAndUserProgressV2 Jeu,
        DateTimeOffset DateChargement
    );

    private static readonly Uri AdresseBaseApi = new("https://retroachievements.org/API/");
    private static readonly TimeSpan DureeCacheConsoles = TimeSpan.FromHours(6);
    private static readonly TimeSpan DureeCacheJeuxSysteme = TimeSpan.FromHours(12);
    private static readonly TimeSpan DureeCacheJeuUtilisateur = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = AdresseBaseApi,
        Timeout = TimeSpan.FromSeconds(15),
    };
    private static List<ConsoleV2> _cacheConsoles = [];
    private static DateTimeOffset _dateMiseEnCacheConsoles = DateTimeOffset.MinValue;
    private static readonly Dictionary<int, JeuxSystemeCachees> _cacheJeuxSysteme = [];
    private static readonly Dictionary<string, JeuUtilisateurCache> _cacheJeuxUtilisateur = [];

    /// <summary>
    /// Récupère le profil utilisateur minimal nécessaire à l'affichage du jeu en cours.
    /// </summary>
    public static async Task<UserProfileV2> ObtenirProfilUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        string cheminRequete =
            $"API_GetUserProfile.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );

        if (!reponse.IsSuccessStatusCode)
        {
            string contenuErreur = await reponse.Content.ReadAsStringAsync(jetonAnnulation);

            if (
                reponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                || ContientIndicationUtilisateurInaccessible(contenuErreur)
            )
            {
                throw new UtilisateurRetroAchievementsInaccessibleException(pseudo);
            }

            reponse.EnsureSuccessStatusCode();
        }

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserProfileV2? profil = await JsonSerializer.DeserializeAsync<UserProfileV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        if (profil is null || string.IsNullOrWhiteSpace(profil.User))
        {
            throw new UtilisateurRetroAchievementsInaccessibleException(pseudo);
        }

        return profil;
    }

    /// <summary>
    /// Récupère le résumé utilisateur utile à l'en-tête de la modale compte.
    /// </summary>
    public static async Task<UserSummaryV2> ObtenirResumeUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        string cheminRequete =
            $"API_GetUserSummary.php?u={Uri.EscapeDataString(pseudo)}&g=3&a=10&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );

        if (!reponse.IsSuccessStatusCode)
        {
            string contenuErreur = await reponse.Content.ReadAsStringAsync(jetonAnnulation);

            if (
                reponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
                || ContientIndicationUtilisateurInaccessible(contenuErreur)
            )
            {
                throw new UtilisateurRetroAchievementsInaccessibleException(pseudo);
            }

            reponse.EnsureSuccessStatusCode();
        }

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserSummaryV2? resume = await JsonSerializer.DeserializeAsync<UserSummaryV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        ) ?? throw new InvalidOperationException(
                "La réponse du résumé utilisateur RetroAchievements est vide."
            );
        return resume;
    }

    /// <summary>
    /// Récupère les points hardcore et softcore d'un utilisateur.
    /// </summary>
    public static async Task<UserPointsV2> ObtenirPointsUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);

        string cheminRequete =
            $"API_GetUserPoints.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserPointsV2? points = await JsonSerializer.DeserializeAsync<UserPointsV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return points ?? new UserPointsV2();
    }

    /// <summary>
    /// Récupère les récompenses visibles d'un utilisateur.
    /// </summary>
    public static async Task<UserAwardsResponseV2> ObtenirRecompensesUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);

        string cheminRequete =
            $"API_GetUserAwards.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserAwardsResponseV2? recompenses =
            await JsonSerializer.DeserializeAsync<UserAwardsResponseV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return recompenses ?? new UserAwardsResponseV2();
    }

    /// <summary>
    /// Récupère la progression d'un utilisateur sur une liste précise de jeux.
    /// </summary>
    public static async Task<UserProgressResponseV2> ObtenirProgressionUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        IEnumerable<int> identifiantsJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);

        List<int> ids = [.. identifiantsJeu.Where(id => id > 0).Distinct()];

        if (ids.Count == 0)
        {
            throw new ArgumentException(
                "Au moins un identifiant de jeu valide est requis.",
                nameof(identifiantsJeu)
            );
        }

        string listeIds = string.Join(",", ids);
        string cheminRequete =
            $"API_GetUserProgress.php?u={Uri.EscapeDataString(pseudo)}&i={Uri.EscapeDataString(listeIds)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserProgressResponseV2? progression =
            await JsonSerializer.DeserializeAsync<UserProgressResponseV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return progression ?? [];
    }

    /// <summary>
    /// Récupère les claims d'un utilisateur.
    /// </summary>
    public static async Task<IReadOnlyList<UserClaimV2>> ObtenirClaimsUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);

        string cheminRequete =
            $"API_GetUserClaims.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<UserClaimV2>? claims = await JsonSerializer.DeserializeAsync<List<UserClaimV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return claims ?? [];
    }

    /// <summary>
    /// Récupère les demandes de sets d'un utilisateur.
    /// </summary>
    public static async Task<UserSetRequestsResponseV2> ObtenirDemandesSetsUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);

        string cheminRequete =
            $"API_GetUserSetRequests.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        UserSetRequestsResponseV2? demandes =
            await JsonSerializer.DeserializeAsync<UserSetRequestsResponseV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return demandes ?? new UserSetRequestsResponseV2();
    }

    /// <summary>
    /// Récupère les données du jeu ciblé ainsi que la progression de l'utilisateur.
    /// </summary>
    public static async Task<GameInfoAndUserProgressV2> ObtenirJeuEtProgressionUtilisateurAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        if (identifiantJeu <= 0)
        {
            throw new ArgumentException(
                "L'identifiant du jeu est invalide.",
                nameof(identifiantJeu)
            );
        }

        string cleCacheJeuUtilisateur = $"{pseudo.Trim().ToLowerInvariant()}|{identifiantJeu}";
        DateTimeOffset maintenant = DateTimeOffset.UtcNow;

        if (
            _cacheJeuxUtilisateur.TryGetValue(
                cleCacheJeuUtilisateur,
                out JeuUtilisateurCache? jeuCache
            )
            && jeuCache is not null
            && maintenant - jeuCache.DateChargement < DureeCacheJeuUtilisateur
        )
        {
            return jeuCache.Jeu;
        }

        string cheminRequete =
            $"API_GetGameInfoAndUserProgress.php?u={Uri.EscapeDataString(pseudo)}&g={identifiantJeu}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameInfoAndUserProgressV2? jeu =
            await JsonSerializer.DeserializeAsync<GameInfoAndUserProgressV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            ) ?? throw new InvalidOperationException("La réponse du jeu RetroAchievements est vide.");
        if (string.IsNullOrWhiteSpace(jeu.Title))
        {
            throw new InvalidOperationException(
                "Les données du jeu RetroAchievements sont incomplètes."
            );
        }

        _cacheJeuxUtilisateur[cleCacheJeuUtilisateur] = new JeuUtilisateurCache(jeu, maintenant);

        return jeu;
    }

    /// <summary>
    /// Récupère le résumé simple d'un jeu.
    /// </summary>
    public static async Task<GameSummaryV2> ObtenirResumeJeuAsync(
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);
        ValiderIdentifiantJeu(identifiantJeu);

        string cheminRequete =
            $"API_GetGame.php?i={identifiantJeu}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameSummaryV2? jeu = await JsonSerializer.DeserializeAsync<GameSummaryV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return jeu ?? new GameSummaryV2();
    }

    /// <summary>
    /// Récupère les détails étendus d'un jeu.
    /// </summary>
    public static async Task<GameExtendedDetailsV2> ObtenirDetailsEtendusJeuAsync(
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);
        ValiderIdentifiantJeu(identifiantJeu);

        string cheminRequete =
            $"API_GetGameExtended.php?i={identifiantJeu}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameExtendedDetailsV2? jeu = await JsonSerializer.DeserializeAsync<GameExtendedDetailsV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return jeu ?? new GameExtendedDetailsV2();
    }

    /// <summary>
    /// Récupère les statistiques de progression d'un jeu.
    /// </summary>
    public static async Task<GameProgressionV2> ObtenirProgressionJeuAsync(
        string cleApiWeb,
        int identifiantJeu,
        bool prefererHardcore = false,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);
        ValiderIdentifiantJeu(identifiantJeu);

        string parametreHardcore = prefererHardcore ? "&h=1" : string.Empty;
        string cheminRequete =
            $"API_GetGameProgression.php?i={identifiantJeu}{parametreHardcore}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameProgressionV2? progression = await JsonSerializer.DeserializeAsync<GameProgressionV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return progression ?? new GameProgressionV2();
    }

    /// <summary>
    /// Récupère la distribution des déblocages d'un jeu.
    /// </summary>
    public static async Task<GameUnlockDistributionV2> ObtenirDistributionSuccesJeuAsync(
        string cleApiWeb,
        int identifiantJeu,
        bool hardcore = false,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);
        ValiderIdentifiantJeu(identifiantJeu);

        string parametreHardcore = hardcore ? "&h=1" : string.Empty;
        string cheminRequete =
            $"API_GetAchievementDistribution.php?i={identifiantJeu}{parametreHardcore}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameUnlockDistributionV2? distribution =
            await JsonSerializer.DeserializeAsync<GameUnlockDistributionV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return distribution ?? [];
    }

    /// <summary>
    /// Récupère le rang et le score d'un utilisateur sur un jeu.
    /// </summary>
    public static async Task<IReadOnlyList<GameRankAndScoreEntryV2>> ObtenirRangEtScoreJeuAsync(
        string pseudo,
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderIdentificationUtilisateur(pseudo, cleApiWeb);
        ValiderIdentifiantJeu(identifiantJeu);

        string cheminRequete =
            $"API_GetUserGameRankAndScore.php?u={Uri.EscapeDataString(pseudo)}&g={identifiantJeu}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<GameRankAndScoreEntryV2>? resultats = await JsonSerializer.DeserializeAsync<
            List<GameRankAndScoreEntryV2>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return resultats ?? [];
    }

    /// <summary>
    /// Récupère les récompenses de jeu récemment attribuées sur le site.
    /// </summary>
    public static async Task<RecentGameAwardsResponseV2> ObtenirRecompensesJeuxRecentesAsync(
        string cleApiWeb,
        DateOnly? dateDepart = null,
        int nombreResultats = 25,
        int decalage = 0,
        string? naturesRecompense = null,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        string cheminRequete =
            $"API_GetRecentGameAwards.php?c={nombreResultats}&o={decalage}&y={Uri.EscapeDataString(cleApiWeb)}";

        if (dateDepart is not null)
        {
            cheminRequete += $"&d={dateDepart.Value:yyyy-MM-dd}";
        }

        if (!string.IsNullOrWhiteSpace(naturesRecompense))
        {
            cheminRequete += $"&k={Uri.EscapeDataString(naturesRecompense)}";
        }

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        RecentGameAwardsResponseV2? recompenses =
            await JsonSerializer.DeserializeAsync<RecentGameAwardsResponseV2>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return recompenses ?? new RecentGameAwardsResponseV2();
    }

    /// <summary>
    /// Récupère l'ensemble des claims actives du site.
    /// </summary>
    public static async Task<IReadOnlyList<UserClaimV2>> ObtenirClaimsActivesAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        string cheminRequete = $"API_GetActiveClaims.php?y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<UserClaimV2>? claims = await JsonSerializer.DeserializeAsync<List<UserClaimV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return claims ?? [];
    }

    /// <summary>
    /// Récupère l'ensemble des claims inactives du site pour un type donné.
    /// </summary>
    public static async Task<IReadOnlyList<UserClaimV2>> ObtenirClaimsInactivesAsync(
        string cleApiWeb,
        int natureClaim = 1,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        if (natureClaim <= 0)
        {
            throw new ArgumentException("La nature de claim est invalide.", nameof(natureClaim));
        }

        string cheminRequete =
            $"API_GetClaims.php?k={natureClaim}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<UserClaimV2>? claims = await JsonSerializer.DeserializeAsync<List<UserClaimV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return claims ?? [];
    }

    /// <summary>
    /// Récupère le top 10 des utilisateurs classés par points hardcore.
    /// </summary>
    public static async Task<IReadOnlyList<TopRankedUserV2>> ObtenirTopDixUtilisateursAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        string cheminRequete = $"API_GetTopTenUsers.php?y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<TopRankedUserV2>? utilisateurs = await JsonSerializer.DeserializeAsync<
            List<TopRankedUserV2>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return utilisateurs ?? [];
    }

    /// <summary>
    /// Récupère les empreintes officielles connues par RetroAchievements pour un jeu donné.
    /// </summary>
    public static async Task<IReadOnlyList<GameHashV2>> ObtenirHashesJeuAsync(
        string cleApiWeb,
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        if (identifiantJeu <= 0)
        {
            throw new ArgumentException(
                "L'identifiant du jeu est invalide.",
                nameof(identifiantJeu)
            );
        }

        string cheminRequete =
            $"API_GetGameHashes.php?i={identifiantJeu}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        GameHashesResponseV2? hashes = await JsonSerializer.DeserializeAsync<GameHashesResponseV2>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return hashes?.Results ?? [];
    }

    /// <summary>
    /// Récupère la liste complète des jeux d'un système avec leurs hashes officiels.
    /// </summary>
    public static async Task<IReadOnlyList<GameListEntryV2>> ObtenirJeuxSystemeAvecHashesAsync(
        string cleApiWeb,
        int identifiantConsole,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        if (identifiantConsole <= 0)
        {
            throw new ArgumentException(
                "L'identifiant de la console est invalide.",
                nameof(identifiantConsole)
            );
        }

        if (
            _cacheJeuxSysteme.TryGetValue(identifiantConsole, out JeuxSystemeCachees? jeuxCaches)
            && DateTimeOffset.UtcNow - jeuxCaches.DateMiseEnCacheUtc < DureeCacheJeuxSysteme
        )
        {
            return jeuxCaches.Jeux;
        }

        string cheminRequete =
            $"API_GetGameList.php?i={identifiantConsole}&f=1&h=1&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<GameListEntryV2>? jeux = await JsonSerializer.DeserializeAsync<List<GameListEntryV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        IReadOnlyList<GameListEntryV2> resultat = jeux ?? [];
        _cacheJeuxSysteme[identifiantConsole] = new JeuxSystemeCachees(resultat);
        return resultat;
    }

    /// <summary>
    /// Récupère la liste des consoles et leur icône officielle, avec cache mémoire local.
    /// </summary>
    public static async Task<IReadOnlyList<ConsoleV2>> ObtenirConsolesAsync(
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        if (
            _cacheConsoles.Count > 0
            && DateTimeOffset.UtcNow - _dateMiseEnCacheConsoles < DureeCacheConsoles
        )
        {
            return _cacheConsoles;
        }

        string cheminRequete = $"API_GetConsoleIDs.php?g=1&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<ConsoleV2>? consoles = await JsonSerializer.DeserializeAsync<List<ConsoleV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        _cacheConsoles = consoles ?? [];
        _dateMiseEnCacheConsoles = DateTimeOffset.UtcNow;
        return _cacheConsoles;
    }

    /// <summary>
    /// Récupère la liste complète des systèmes RetroAchievements.
    /// </summary>
    public static async Task<IReadOnlyList<SystemEntryV2>> ObtenirSystemesAsync(
        string cleApiWeb,
        bool seulementActifs = false,
        bool seulementSystemesDeJeu = false,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        string cheminRequete = $"API_GetConsoleIDs.php?y={Uri.EscapeDataString(cleApiWeb)}";

        if (seulementActifs)
        {
            cheminRequete += "&a=1";
        }

        if (seulementSystemesDeJeu)
        {
            cheminRequete += "&g=1";
        }

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<SystemEntryV2>? systemes = await JsonSerializer.DeserializeAsync<List<SystemEntryV2>>(
            fluxReponse,
            OptionsJson,
            jetonAnnulation
        );

        return systemes ?? [];
    }

    /// <summary>
    /// Récupère la liste complète des jeux d'un système, avec ou sans hashes.
    /// </summary>
    public static async Task<IReadOnlyList<SystemGameEntryV2>> ObtenirJeuxSystemeAsync(
        string cleApiWeb,
        int identifiantConsole,
        bool seulementJeuxAvecSucces = false,
        bool inclureHashes = false,
        int nombreResultats = 0,
        int decalage = 0,
        CancellationToken jetonAnnulation = default
    )
    {
        ValiderCleApi(cleApiWeb);

        if (identifiantConsole <= 0)
        {
            throw new ArgumentException(
                "L'identifiant de la console est invalide.",
                nameof(identifiantConsole)
            );
        }

        string cheminRequete =
            $"API_GetGameList.php?i={identifiantConsole}&o={decalage}&c={nombreResultats}&y={Uri.EscapeDataString(cleApiWeb)}";

        if (seulementJeuxAvecSucces)
        {
            cheminRequete += "&f=1";
        }

        if (inclureHashes)
        {
            cheminRequete += "&h=1";
        }

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<SystemGameEntryV2>? jeux = await JsonSerializer.DeserializeAsync<
            List<SystemGameEntryV2>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return jeux ?? [];
    }

    /// <summary>
    /// Récupère la liste des jeux récemment joués pour retrouver un dernier jeu
    /// lorsqu'aucun jeu actif n'est remonté.
    /// </summary>
    public static async Task<IReadOnlyList<RecentlyPlayedGameV2>> ObtenirJeuxRecemmentJouesAsync(
        string pseudo,
        string cleApiWeb,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        string cheminRequete =
            $"API_GetUserRecentlyPlayedGames.php?u={Uri.EscapeDataString(pseudo)}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<RecentlyPlayedGameV2>? jeux = await JsonSerializer.DeserializeAsync<
            List<RecentlyPlayedGameV2>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return jeux ?? [];
    }

    /// <summary>
    /// Récupère les succès débloqués par un utilisateur sur une plage de temps.
    /// </summary>
    public static async Task<IReadOnlyList<AchievementUnlockV2>> ObtenirSuccesDebloquesEntreAsync(
        string pseudo,
        string cleApiWeb,
        DateTimeOffset debut,
        DateTimeOffset fin,
        CancellationToken jetonAnnulation = default
    )
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }

        long horodatageDebut = debut.ToUnixTimeSeconds();
        long horodatageFin = fin.ToUnixTimeSeconds();

        string cheminRequete =
            $"API_GetAchievementsEarnedBetween.php?u={Uri.EscapeDataString(pseudo)}&f={horodatageDebut}&t={horodatageFin}&y={Uri.EscapeDataString(cleApiWeb)}";

        using HttpResponseMessage reponse = await HttpClient.GetAsync(
            cheminRequete,
            jetonAnnulation
        );
        reponse.EnsureSuccessStatusCode();

        await using Stream fluxReponse = await reponse.Content.ReadAsStreamAsync(jetonAnnulation);
        List<AchievementUnlockV2>? succes = await JsonSerializer.DeserializeAsync<
            List<AchievementUnlockV2>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return succes ?? [];
    }

    /// <summary>
    /// Détecte quelques formulations courantes indiquant que l'utilisateur n'existe pas
    /// ou n'est pas accessible.
    /// </summary>
    private static bool ContientIndicationUtilisateurInaccessible(string contenuErreur)
    {
        if (string.IsNullOrWhiteSpace(contenuErreur))
        {
            return false;
        }

        string contenuNormalise = contenuErreur.Trim().ToLowerInvariant();

        return contenuNormalise.Contains("not found", StringComparison.Ordinal)
            || contenuNormalise.Contains("unknown user", StringComparison.Ordinal)
            || contenuNormalise.Contains("user not found", StringComparison.Ordinal)
            || contenuNormalise.Contains("cannot access user", StringComparison.Ordinal)
            || contenuNormalise.Contains("unable to access user", StringComparison.Ordinal)
            || contenuNormalise.Contains("no user", StringComparison.Ordinal);
    }

    private static void ValiderIdentificationUtilisateur(string pseudo, string cleApiWeb)
    {
        if (string.IsNullOrWhiteSpace(pseudo))
        {
            throw new ArgumentException("Le pseudo utilisateur est obligatoire.", nameof(pseudo));
        }

        ValiderCleApi(cleApiWeb);
    }

    private static void ValiderCleApi(string cleApiWeb)
    {
        if (string.IsNullOrWhiteSpace(cleApiWeb))
        {
            throw new ArgumentException(
                "La clé API RetroAchievements est obligatoire.",
                nameof(cleApiWeb)
            );
        }
    }

    private static void ValiderIdentifiantJeu(int identifiantJeu)
    {
        if (identifiantJeu <= 0)
        {
            throw new ArgumentException(
                "L'identifiant du jeu est invalide.",
                nameof(identifiantJeu)
            );
        }
    }

    /// <summary>
    /// Représente une liste de jeux système gardée en mémoire pendant une durée étendue.
    /// </summary>
    private sealed class JeuxSystemeCachees(IReadOnlyList<GameListEntryV2> jeux)
    {
        public IReadOnlyList<GameListEntryV2> Jeux { get; } = jeux;

        public DateTimeOffset DateMiseEnCacheUtc { get; } = DateTimeOffset.UtcNow;
    }
}
