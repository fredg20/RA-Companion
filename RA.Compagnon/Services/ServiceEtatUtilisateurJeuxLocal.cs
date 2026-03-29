using System.IO;
using System.Text.Json;
using RA.Compagnon.Modeles.Api.V2.Game;
using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon.Services;

public sealed class ServiceEtatUtilisateurJeuxLocal
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const string ExtensionTemporaire = ".tmp";
    private readonly SemaphoreSlim _verrou = new(1, 1);
    private EtatUtilisateurJeuxLocal? _etat;

    public async Task<EtatJeuUtilisateurLocal?> ObtenirJeuAsync(
        int identifiantJeu,
        CancellationToken jetonAnnulation = default
    )
    {
        if (identifiantJeu <= 0)
        {
            return null;
        }

        EtatUtilisateurJeuxLocal etat = await ChargerEtatAsync(jetonAnnulation);
        return etat.Jeux.FirstOrDefault(jeu => jeu.GameId == identifiantJeu);
    }

    public async Task<(EtatJeuUtilisateurLocal? Precedent, EtatJeuUtilisateurLocal Courant)>
        EnregistrerEtatJeuAsync(
            GameInfoAndUserProgressV2 jeu,
            CancellationToken jetonAnnulation = default
        )
    {
        await _verrou.WaitAsync(jetonAnnulation);

        try
        {
            EtatUtilisateurJeuxLocal etat = await ChargerEtatInterneAsync(jetonAnnulation);
            EtatJeuUtilisateurLocal? precedent = etat.Jeux.FirstOrDefault(item =>
                item.GameId == jeu.Id
            );
            EtatJeuUtilisateurLocal courant = ConstruireEtatJeu(jeu);

            if (precedent is null)
            {
                etat.Jeux.Add(courant);
            }
            else
            {
                int index = etat.Jeux.IndexOf(precedent);
                etat.Jeux[index] = courant;
            }

            etat.DateMajUtc = DateTimeOffset.UtcNow;
            _etat = etat;
            await SauvegarderEtatAsync(etat, jetonAnnulation);
            return (precedent, courant);
        }
        finally
        {
            _verrou.Release();
        }
    }

    public static EtatJeuUtilisateurLocal ConstruireEtatJeu(GameInfoAndUserProgressV2 jeu)
    {
        List<EtatSuccesUtilisateurLocal> succes =
        [
            .. jeu.Succes.Values.Select(item => new EtatSuccesUtilisateurLocal
            {
                AchievementId = item.Id,
                EstDebloque =
                    !string.IsNullOrWhiteSpace(item.DateEarned)
                    || !string.IsNullOrWhiteSpace(item.DateEarnedHardcore),
                EstHardcore = !string.IsNullOrWhiteSpace(item.DateEarnedHardcore),
                DateDeblocageUtc = (item.DateEarned ?? string.Empty).Trim(),
                DateDeblocageHardcoreUtc = (item.DateEarnedHardcore ?? string.Empty).Trim(),
            }).OrderBy(item => item.AchievementId)
        ];

        EtatSuccesUtilisateurLocal? dernierSucces = succes
            .Where(item => item.EstDebloque)
            .OrderByDescending(item => item.DateDeblocageHardcoreUtc)
            .ThenByDescending(item => item.DateDeblocageUtc)
            .FirstOrDefault();

        double progressionPourcentage = jeu.NumAchievements <= 0
            ? 0
            : (double)jeu.NumAwardedToUser / jeu.NumAchievements * 100d;

        return new EtatJeuUtilisateurLocal
        {
            GameId = jeu.Id,
            DerniereObservationUtc = DateTimeOffset.UtcNow,
            NbSuccesDebloques = jeu.NumAwardedToUser,
            NbSuccesDebloquesHardcore = jeu.NumAwardedToUserHardcore,
            ProgressionPourcentage = progressionPourcentage,
            DernierSuccesDetecteId = dernierSucces?.AchievementId ?? 0,
            DernierSuccesDetecteUtc = dernierSucces?.EstHardcore == true
                ? dernierSucces.DateDeblocageHardcoreUtc
                : dernierSucces?.DateDeblocageUtc ?? string.Empty,
            Succes = succes,
        };
    }

    private async Task<EtatUtilisateurJeuxLocal> ChargerEtatAsync(CancellationToken jetonAnnulation)
    {
        await _verrou.WaitAsync(jetonAnnulation);

        try
        {
            return await ChargerEtatInterneAsync(jetonAnnulation);
        }
        finally
        {
            _verrou.Release();
        }
    }

    private async Task<EtatUtilisateurJeuxLocal> ChargerEtatInterneAsync(
        CancellationToken jetonAnnulation
    )
    {
        if (_etat is not null)
        {
            return _etat;
        }

        string chemin = ObtenirCheminEtat();
        string cheminLecture = File.Exists(chemin) ? chemin : chemin + ExtensionTemporaire;

        if (!File.Exists(cheminLecture))
        {
            _etat = new EtatUtilisateurJeuxLocal();
            return _etat;
        }

        try
        {
            await using FileStream flux = File.OpenRead(cheminLecture);
            EtatUtilisateurJeuxLocal? etat =
                await JsonSerializer.DeserializeAsync<EtatUtilisateurJeuxLocal>(
                    flux,
                    OptionsJson,
                    jetonAnnulation
                );
            _etat = etat ?? new EtatUtilisateurJeuxLocal();
        }
        catch (JsonException)
        {
            _etat = new EtatUtilisateurJeuxLocal();
        }

        return _etat;
    }

    private static async Task SauvegarderEtatAsync(
        EtatUtilisateurJeuxLocal etat,
        CancellationToken jetonAnnulation
    )
    {
        string chemin = ObtenirCheminEtat();
        string cheminTemporaire = chemin + ExtensionTemporaire;
        Directory.CreateDirectory(Path.GetDirectoryName(chemin)!);

        await using (FileStream flux = File.Create(cheminTemporaire))
        {
            await JsonSerializer.SerializeAsync(flux, etat, OptionsJson, jetonAnnulation);
            await flux.FlushAsync(jetonAnnulation);
        }

        if (File.Exists(chemin))
        {
            File.Delete(chemin);
        }

        File.Move(cheminTemporaire, chemin);
    }

    private static string ObtenirCheminEtat()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RA-Compagnon",
            "cache",
            "etat-utilisateur-jeux.json"
        );
    }
}
