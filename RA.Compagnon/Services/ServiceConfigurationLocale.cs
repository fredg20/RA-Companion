using System.IO;
using System.Text.Json;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

/// <summary>
/// Gère le chargement et la sauvegarde de la configuration locale de l'application.
/// </summary>
public sealed class ServiceConfigurationLocale
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const string ExtensionTemporaire = ".tmp";
    private readonly SemaphoreSlim _verrouSauvegarde = new(1, 1);
    private readonly SemaphoreSlim _verrouUtilisateur = new(1, 1);

    /// <summary>
    /// Chemin complet du fichier dédié aux informations utilisateur.
    /// </summary>
    public string CheminFichierUtilisateur =>
        Path.Combine(ObtenirDossierConfiguration(), "user.json");

    /// <summary>
    /// Chemin complet du fichier de configuration.
    /// </summary>
    public string CheminFichierConfiguration =>
        Path.Combine(ObtenirDossierConfiguration(), "configuration.json");

    /// <summary>
    /// Chemin complet du fichier dédié au dernier jeu affiché.
    /// </summary>
    public string CheminFichierJeu => Path.Combine(ObtenirDossierConfiguration(), "game.json");

    /// <summary>
    /// Chemin complet du fichier dédié au dernier rétrosuccès affiché.
    /// </summary>
    public string CheminFichierSucces =>
        Path.Combine(ObtenirDossierConfiguration(), "achievement.json");

    /// <summary>
    /// Chemin complet du fichier dédié à la grille des rétrosuccès affichés.
    /// </summary>
    public string CheminFichierListeSucces =>
        Path.Combine(ObtenirDossierConfiguration(), "achievements_list.json");

    /// <summary>
    /// Charge la configuration locale si elle existe.
    /// </summary>
    public async Task<ConfigurationConnexion> ChargerAsync()
    {
        FinaliserFichierTemporaireSiNecessaire(CheminFichierUtilisateur);
        FinaliserFichierTemporaireSiNecessaire(CheminFichierConfiguration);
        FinaliserFichierTemporaireSiNecessaire(CheminFichierJeu);
        FinaliserFichierTemporaireSiNecessaire(CheminFichierSucces);
        FinaliserFichierTemporaireSiNecessaire(CheminFichierListeSucces);

        ConfigurationConnexion configuration = new();

        if (File.Exists(CheminFichierConfiguration))
        {
            configuration =
                await ChargerJsonAsync<ConfigurationConnexion>(CheminFichierConfiguration)
                ?? new ConfigurationConnexion();
        }

        EtatUtilisateurLocal? utilisateur = await ChargerEtatUtilisateurAsync();
        configuration.Pseudo = utilisateur?.Pseudo ?? string.Empty;
        configuration.CleApiWeb = utilisateur?.CleApiWeb ?? string.Empty;
        configuration.DernierJeuAffiche = await ChargerEtatJeuAsync();
        configuration.DernierSuccesAffiche = await ChargerEtatSuccesAsync();
        configuration.DerniereListeSuccesAffichee = await ChargerEtatListeSuccesAsync();
        return configuration;
    }

    /// <summary>
    /// Sauvegarde la configuration locale sur disque.
    /// </summary>
    public async Task SauvegarderAsync(ConfigurationConnexion configuration)
    {
        await SauvegarderUtilisateurAsync(configuration);
        await SauvegarderEtatApplicationAsync(configuration);
    }

    /// <summary>
    /// Sauvegarde les informations utilisateur sur disque.
    /// </summary>
    public async Task SauvegarderUtilisateurAsync(ConfigurationConnexion configuration)
    {
        await _verrouUtilisateur.WaitAsync();

        try
        {
            Directory.CreateDirectory(ObtenirDossierConfiguration());
            await SauvegarderJsonAsync(
                CheminFichierUtilisateur,
                new EtatUtilisateurLocal
                {
                    Pseudo = configuration.Pseudo,
                    CleApiWeb = configuration.CleApiWeb,
                }
            );
        }
        finally
        {
            _verrouUtilisateur.Release();
        }
    }

    /// <summary>
    /// Sauvegarde l'état visuel de l'application sur disque.
    /// </summary>
    public async Task SauvegarderEtatApplicationAsync(ConfigurationConnexion configuration)
    {
        await _verrouSauvegarde.WaitAsync();

        try
        {
            Directory.CreateDirectory(ObtenirDossierConfiguration());
            await SauvegarderJsonAsync(CheminFichierConfiguration, configuration);
            await SauvegarderOuSupprimerAsync(CheminFichierJeu, configuration.DernierJeuAffiche);
            await SauvegarderOuSupprimerAsync(
                CheminFichierSucces,
                configuration.DernierSuccesAffiche
            );
            await SauvegarderOuSupprimerAsync(
                CheminFichierListeSucces,
                configuration.DerniereListeSuccesAffichee
            );
        }
        finally
        {
            _verrouSauvegarde.Release();
        }
    }

    /// <summary>
    /// Charge l'état de l'utilisateur depuis le fichier dédié ou l'ancien fichier de configuration.
    /// </summary>
    private async Task<EtatUtilisateurLocal?> ChargerEtatUtilisateurAsync()
    {
        if (File.Exists(CheminFichierUtilisateur))
        {
            return await ChargerJsonAsync<EtatUtilisateurLocal>(CheminFichierUtilisateur);
        }

        if (!File.Exists(CheminFichierConfiguration))
        {
            return null;
        }

        try
        {
            await using FileStream fluxConfiguration = File.OpenRead(CheminFichierConfiguration);

            if (fluxConfiguration.Length == 0)
            {
                return null;
            }

            using JsonDocument document = await JsonDocument.ParseAsync(fluxConfiguration);
            JsonElement racine = document.RootElement;

            return new EtatUtilisateurLocal
            {
                Pseudo = ExtraireValeurString(racine, nameof(ConfigurationConnexion.Pseudo)),
                CleApiWeb = ExtraireValeurString(racine, nameof(ConfigurationConnexion.CleApiWeb)),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Charge l'état du dernier jeu affiché depuis le fichier dédié ou l'ancien fichier de configuration.
    /// </summary>
    private async Task<EtatJeuAfficheLocal?> ChargerEtatJeuAsync()
    {
        if (File.Exists(CheminFichierJeu))
        {
            return await ChargerJsonAsync<EtatJeuAfficheLocal>(CheminFichierJeu);
        }

        if (!File.Exists(CheminFichierConfiguration))
        {
            return null;
        }

        try
        {
            await using FileStream fluxConfiguration = File.OpenRead(CheminFichierConfiguration);

            if (fluxConfiguration.Length == 0)
            {
                return null;
            }

            using JsonDocument document = await JsonDocument.ParseAsync(fluxConfiguration);

            if (
                !document.RootElement.TryGetProperty(
                    nameof(ConfigurationConnexion.DernierJeuAffiche),
                    out JsonElement elementJeu
                ) || elementJeu.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            )
            {
                return null;
            }

            return elementJeu.Deserialize<EtatJeuAfficheLocal>(OptionsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Charge l'état du dernier rétrosuccès affiché depuis son fichier dédié.
    /// </summary>
    private async Task<EtatSuccesAfficheLocal?> ChargerEtatSuccesAsync()
    {
        return File.Exists(CheminFichierSucces)
            ? await ChargerJsonAsync<EtatSuccesAfficheLocal>(CheminFichierSucces)
            : null;
    }

    /// <summary>
    /// Charge l'état de la dernière grille de rétrosuccès affichée depuis son fichier dédié.
    /// </summary>
    private async Task<EtatListeSuccesAfficheeLocal?> ChargerEtatListeSuccesAsync()
    {
        return File.Exists(CheminFichierListeSucces)
            ? await ChargerJsonAsync<EtatListeSuccesAfficheeLocal>(CheminFichierListeSucces)
            : null;
    }

    /// <summary>
    /// Retourne le dossier standard de configuration de l'application.
    /// </summary>
    private static string ObtenirDossierConfiguration()
    {
        string dossierAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
        );
        return Path.Combine(dossierAppData, "RA-Compagnon");
    }

    /// <summary>
    /// Extrait une valeur texte depuis un objet JSON.
    /// </summary>
    private static string ExtraireValeurString(JsonElement racine, string nomPropriete)
    {
        if (
            !racine.TryGetProperty(nomPropriete, out JsonElement element)
            || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        )
        {
            return string.Empty;
        }

        return element.GetString() ?? string.Empty;
    }

    /// <summary>
    /// Charge un fichier JSON en tolérant les fichiers vides ou corrompus.
    /// </summary>
    private static async Task<T?> ChargerJsonAsync<T>(string cheminFichier)
    {
        string cheminLecture = DeterminerCheminLecture(cheminFichier);

        if (!File.Exists(cheminLecture))
        {
            return default;
        }

        try
        {
            await using FileStream fluxLecture = File.OpenRead(cheminLecture);

            if (fluxLecture.Length == 0)
            {
                return default;
            }

            return await JsonSerializer.DeserializeAsync<T>(fluxLecture, OptionsJson);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// Sauvegarde un fichier JSON en écriture atomique pour éviter les fichiers vides.
    /// </summary>
    private static async Task SauvegarderJsonAsync<T>(string cheminFichier, T donnees)
    {
        string cheminTemporaire = cheminFichier + ExtensionTemporaire;
        await using (FileStream fluxEcriture = File.Create(cheminTemporaire))
        {
            await JsonSerializer.SerializeAsync(fluxEcriture, donnees, OptionsJson);
            await fluxEcriture.FlushAsync();
        }

        if (File.Exists(cheminFichier))
        {
            File.Delete(cheminFichier);
        }

        File.Move(cheminTemporaire, cheminFichier);
    }

    /// <summary>
    /// Sauvegarde un fichier JSON s'il existe une valeur, sinon supprime le fichier cible.
    /// </summary>
    private static async Task SauvegarderOuSupprimerAsync<T>(string cheminFichier, T? donnees)
        where T : class
    {
        if (donnees is null)
        {
            if (File.Exists(cheminFichier))
            {
                File.Delete(cheminFichier);
            }

            string cheminTemporaire = cheminFichier + ExtensionTemporaire;

            if (File.Exists(cheminTemporaire))
            {
                File.Delete(cheminTemporaire);
            }

            return;
        }

        await SauvegarderJsonAsync(cheminFichier, donnees);
    }

    /// <summary>
    /// Retourne le meilleur chemin de lecture disponible, y compris un fichier temporaire de secours.
    /// </summary>
    private static string DeterminerCheminLecture(string cheminFichier)
    {
        return File.Exists(cheminFichier) ? cheminFichier : cheminFichier + ExtensionTemporaire;
    }

    /// <summary>
    /// Promote un fichier temporaire valide quand le fichier final manque.
    /// </summary>
    private static void FinaliserFichierTemporaireSiNecessaire(string cheminFichier)
    {
        string cheminTemporaire = cheminFichier + ExtensionTemporaire;

        if (!File.Exists(cheminTemporaire))
        {
            return;
        }

        if (File.Exists(cheminFichier))
        {
            File.Delete(cheminTemporaire);
            return;
        }

        File.Move(cheminTemporaire, cheminFichier);
    }
}
