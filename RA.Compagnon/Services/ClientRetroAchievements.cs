using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RA.Compagnon.Modeles.Api;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Encapsule les appels minimaux à l'API RetroAchievements pour le MVP.
/// </summary>
public sealed class ClientRetroAchievements
{
    private sealed record JeuUtilisateurCache(
        JeuUtilisateurRetroAchievements Jeu,
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
    private static List<ConsoleRetroAchievements> _cacheConsoles = [];
    private static DateTimeOffset _dateMiseEnCacheConsoles = DateTimeOffset.MinValue;
    private static readonly Dictionary<int, JeuxSystemeCachees> _cacheJeuxSysteme = [];
    private static readonly Dictionary<string, JeuUtilisateurCache> _cacheJeuxUtilisateur = [];

    /// <summary>
    /// Récupère le profil utilisateur minimal nécessaire à l'affichage du jeu en cours.
    /// </summary>
    public static async Task<ProfilUtilisateurRetroAchievements> ObtenirProfilUtilisateurAsync(
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
        ProfilUtilisateurRetroAchievements? profil =
            await JsonSerializer.DeserializeAsync<ProfilUtilisateurRetroAchievements>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        if (profil is null || string.IsNullOrWhiteSpace(profil.NomUtilisateur))
        {
            throw new UtilisateurRetroAchievementsInaccessibleException(pseudo);
        }

        return profil;
    }

    /// <summary>
    /// Récupère le résumé utilisateur utile à l'en-tête de la modale compte.
    /// </summary>
    public static async Task<ResumeUtilisateurRetroAchievements> ObtenirResumeUtilisateurAsync(
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
            $"API_GetUserSummary.php?u={Uri.EscapeDataString(pseudo)}&g=0&a=0&y={Uri.EscapeDataString(cleApiWeb)}";

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
        ResumeUtilisateurRetroAchievements? resume =
            await JsonSerializer.DeserializeAsync<ResumeUtilisateurRetroAchievements>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        if (resume is null)
        {
            throw new InvalidOperationException(
                "La réponse du résumé utilisateur RetroAchievements est vide."
            );
        }

        return resume;
    }

    /// <summary>
    /// Récupère les données du jeu ciblé ainsi que la progression de l'utilisateur.
    /// </summary>
    public static async Task<JeuUtilisateurRetroAchievements> ObtenirJeuEtProgressionUtilisateurAsync(
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
        JeuUtilisateurRetroAchievements? jeu =
            await JsonSerializer.DeserializeAsync<JeuUtilisateurRetroAchievements>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        if (jeu is null)
        {
            throw new InvalidOperationException("La réponse du jeu RetroAchievements est vide.");
        }

        if (string.IsNullOrWhiteSpace(jeu.Titre))
        {
            throw new InvalidOperationException(
                "Les données du jeu RetroAchievements sont incomplètes."
            );
        }

        _cacheJeuxUtilisateur[cleCacheJeuUtilisateur] = new JeuUtilisateurCache(jeu, maintenant);

        return jeu;
    }

    /// <summary>
    /// Récupère les empreintes officielles connues par RetroAchievements pour un jeu donné.
    /// </summary>
    public static async Task<IReadOnlyList<HashJeuRetroAchievements>> ObtenirHashesJeuAsync(
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
        ReponseHashesJeuRetroAchievements? hashes =
            await JsonSerializer.DeserializeAsync<ReponseHashesJeuRetroAchievements>(
                fluxReponse,
                OptionsJson,
                jetonAnnulation
            );

        return hashes?.Resultats ?? [];
    }

    /// <summary>
    /// Récupère la liste complète des jeux d'un système avec leurs hashes officiels.
    /// </summary>
    public static async Task<
        IReadOnlyList<JeuSystemeRetroAchievements>
    > ObtenirJeuxSystemeAvecHashesAsync(
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
        List<JeuSystemeRetroAchievements>? jeux = await JsonSerializer.DeserializeAsync<
            List<JeuSystemeRetroAchievements>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        IReadOnlyList<JeuSystemeRetroAchievements> resultat = jeux ?? [];
        _cacheJeuxSysteme[identifiantConsole] = new JeuxSystemeCachees(resultat);
        return resultat;
    }

    /// <summary>
    /// Récupère la liste des consoles et leur icône officielle, avec cache mémoire local.
    /// </summary>
    public static async Task<IReadOnlyList<ConsoleRetroAchievements>> ObtenirConsolesAsync(
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
        List<ConsoleRetroAchievements>? consoles = await JsonSerializer.DeserializeAsync<
            List<ConsoleRetroAchievements>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        _cacheConsoles = consoles ?? [];
        _dateMiseEnCacheConsoles = DateTimeOffset.UtcNow;
        return _cacheConsoles;
    }

    /// <summary>
    /// Récupère la liste des jeux récemment joués pour retrouver un dernier jeu
    /// lorsqu'aucun jeu actif n'est remonté.
    /// </summary>
    public static async Task<
        IReadOnlyList<JeuRecemmentJoueRetroAchievements>
    > ObtenirJeuxRecemmentJouesAsync(
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
        List<JeuRecemmentJoueRetroAchievements>? jeux = await JsonSerializer.DeserializeAsync<
            List<JeuRecemmentJoueRetroAchievements>
        >(fluxReponse, OptionsJson, jetonAnnulation);

        return jeux ?? [];
    }

    /// <summary>
    /// Récupère les succès débloqués par un utilisateur sur une plage de temps.
    /// </summary>
    public static async Task<
        IReadOnlyList<SuccesRecentRetroAchievements>
    > ObtenirSuccesDebloquesEntreAsync(
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
        List<SuccesRecentRetroAchievements>? succes = await JsonSerializer.DeserializeAsync<
            List<SuccesRecentRetroAchievements>
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

    /// <summary>
    /// Représente une liste de jeux système gardée en mémoire pendant une durée étendue.
    /// </summary>
    private sealed class JeuxSystemeCachees
    {
        public JeuxSystemeCachees(IReadOnlyList<JeuSystemeRetroAchievements> jeux)
        {
            Jeux = jeux;
            DateMiseEnCacheUtc = DateTimeOffset.UtcNow;
        }

        public IReadOnlyList<JeuSystemeRetroAchievements> Jeux { get; }

        public DateTimeOffset DateMiseEnCacheUtc { get; }
    }
}
