using System.IO;
using System.Text.Json;
using RA.Compagnon.Modeles.Local;

namespace RA.Compagnon.Services;

public sealed class ServiceConfigurationLocale
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const string ExtensionTemporaire = ".tmp";
    private readonly SemaphoreSlim _verrouSauvegarde = new(1, 1);
    private readonly SemaphoreSlim _verrouUtilisateur = new(1, 1);

    public static string CheminFichierUtilisateur =>
        Path.Combine(ObtenirDossierConfiguration(), "user.json");

    public static string CheminFichierConfiguration =>
        Path.Combine(ObtenirDossierConfiguration(), "configuration.json");

    public static string CheminFichierJeu =>
        Path.Combine(ObtenirDossierConfiguration(), "game.json");

    public static string CheminFichierSucces =>
        Path.Combine(ObtenirDossierConfiguration(), "achievement.json");

    public static string CheminFichierListeSucces =>
        Path.Combine(ObtenirDossierConfiguration(), "achievements_list.json");

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
        bool etatNormalise = NormaliserEtatApplication(configuration);

        if (etatNormalise)
        {
            await SauvegarderEtatApplicationAsync(configuration);
        }

        return configuration;
    }

    public async Task SauvegarderAsync(ConfigurationConnexion configuration)
    {
        await SauvegarderUtilisateurAsync(configuration);
        await SauvegarderEtatApplicationAsync(configuration);
    }

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

    private static async Task<EtatUtilisateurLocal?> ChargerEtatUtilisateurAsync()
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

    private static async Task<EtatJeuAfficheLocal?> ChargerEtatJeuAsync()
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

    private static async Task<EtatSuccesAfficheLocal?> ChargerEtatSuccesAsync()
    {
        return File.Exists(CheminFichierSucces)
            ? await ChargerJsonAsync<EtatSuccesAfficheLocal>(CheminFichierSucces)
            : null;
    }

    private static async Task<EtatListeSuccesAfficheeLocal?> ChargerEtatListeSuccesAsync()
    {
        return File.Exists(CheminFichierListeSucces)
            ? await ChargerJsonAsync<EtatListeSuccesAfficheeLocal>(CheminFichierListeSucces)
            : null;
    }

    private static string ObtenirDossierConfiguration()
    {
        string dossierAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
        );
        return Path.Combine(dossierAppData, "RA-Compagnon");
    }

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

    private static string DeterminerCheminLecture(string cheminFichier)
    {
        return File.Exists(cheminFichier) ? cheminFichier : cheminFichier + ExtensionTemporaire;
    }

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

    private static bool NormaliserEtatApplication(ConfigurationConnexion configuration)
    {
        bool modifie = false;
        configuration.EmplacementsEmulateursManuels ??= [];
        configuration.EmplacementsEmulateursDetectes ??= [];
        List<string> clesInvalides = [];
        List<string> clesDetecteesInvalides = [];

        foreach ((string cle, string valeur) in configuration.EmplacementsEmulateursManuels)
        {
            if (string.IsNullOrWhiteSpace(cle) || string.IsNullOrWhiteSpace(valeur))
            {
                clesInvalides.Add(cle);
            }
        }

        foreach (string cleInvalide in clesInvalides)
        {
            configuration.EmplacementsEmulateursManuels.Remove(cleInvalide);
            modifie = true;
        }

        foreach ((string cle, string valeur) in configuration.EmplacementsEmulateursDetectes)
        {
            if (
                string.IsNullOrWhiteSpace(cle)
                || string.IsNullOrWhiteSpace(valeur)
                || !ServiceSourcesLocalesEmulateurs.CheminExecutableCorrespondEmulateur(cle, valeur)
            )
            {
                clesDetecteesInvalides.Add(cle);
            }
        }

        foreach (string cleInvalide in clesDetecteesInvalides)
        {
            configuration.EmplacementsEmulateursDetectes.Remove(cleInvalide);
            modifie = true;
        }

        string modeAffichageNormalise = string.IsNullOrWhiteSpace(configuration.ModeAffichageSucces)
            ? "Normal"
            : configuration.ModeAffichageSucces.Trim();

        if (
            !string.Equals(
                configuration.ModeAffichageSucces,
                modeAffichageNormalise,
                StringComparison.Ordinal
            )
        )
        {
            configuration.ModeAffichageSucces = modeAffichageNormalise;
            modifie = true;
        }

        if (
            !string.Equals(modeAffichageNormalise, "Normal", StringComparison.Ordinal)
            && !string.Equals(modeAffichageNormalise, "Aléatoire", StringComparison.Ordinal)
            && !string.Equals(modeAffichageNormalise, "Facile", StringComparison.Ordinal)
            && !string.Equals(modeAffichageNormalise, "Difficile", StringComparison.Ordinal)
        )
        {
            configuration.ModeAffichageSucces = "Normal";
            modifie = true;
        }

        EtatJeuAfficheLocal? jeu = configuration.DernierJeuAffiche;

        if (jeu is null)
        {
            if (configuration.DernierSuccesAffiche is not null)
            {
                configuration.DernierSuccesAffiche = null;
                modifie = true;
            }

            if (configuration.DerniereListeSuccesAffichee is not null)
            {
                configuration.DerniereListeSuccesAffichee = null;
                modifie = true;
            }

            return modifie;
        }

        bool cheminRelanceInvalide =
            !string.IsNullOrWhiteSpace(jeu.CheminExecutableEmulateur)
            && (
                string.IsNullOrWhiteSpace(jeu.NomEmulateurRelance)
                || !File.Exists(jeu.CheminExecutableEmulateur)
                || !ServiceSourcesLocalesEmulateurs.CheminExecutableCorrespondEmulateur(
                    jeu.NomEmulateurRelance,
                    jeu.CheminExecutableEmulateur
                )
            );

        bool cheminJeuRelanceInvalide =
            !string.IsNullOrWhiteSpace(jeu.CheminJeuLocal) && !File.Exists(jeu.CheminJeuLocal);

        if (cheminRelanceInvalide || cheminJeuRelanceInvalide)
        {
            jeu.NomEmulateurRelance = string.Empty;
            jeu.CheminExecutableEmulateur = string.Empty;
            jeu.CheminJeuLocal = string.Empty;
            modifie = true;
        }

        int identifiantJeu = jeu.IdentifiantJeu;

        if (
            configuration.DernierSuccesAffiche is not null
            && configuration.DernierSuccesAffiche.IdentifiantJeu != identifiantJeu
        )
        {
            configuration.DernierSuccesAffiche = null;
            modifie = true;
        }

        if (
            configuration.DerniereListeSuccesAffichee is not null
            && configuration.DerniereListeSuccesAffichee.IdentifiantJeu != identifiantJeu
        )
        {
            configuration.DerniereListeSuccesAffichee = null;
            modifie = true;
        }

        return modifie;
    }
}
