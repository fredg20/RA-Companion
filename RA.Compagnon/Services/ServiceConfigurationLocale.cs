using System.IO;
using System.Text.Json;
using RA.Compagnon.Modeles.Local;

/*
 * Gère la lecture, la normalisation et la sauvegarde atomique de la
 * configuration locale de l'application.
 */
namespace RA.Compagnon.Services;

/*
 * Centralise les fichiers persistés du compte, du dernier jeu, du dernier
 * succès affiché et des préférences applicatives.
 */
public sealed class ServiceConfigurationLocale
{
    private static readonly JsonSerializerOptions OptionsJson = new() { WriteIndented = true };
    private const string ExtensionTemporaire = ".tmp";
    private const string ExtensionSauvegarde = ".bak";
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

    /*
     * Charge l'état applicatif complet depuis les fichiers locaux puis
     * normalise les données relues si nécessaire.
     */
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

        bool fichierUtilisateurExiste = File.Exists(CheminFichierUtilisateur);
        EtatUtilisateurLocal? utilisateur = await ChargerEtatUtilisateurAsync();
        configuration.Pseudo = utilisateur?.Pseudo ?? string.Empty;
        configuration.CleApiWeb = utilisateur?.CleApiWeb ?? string.Empty;
        configuration.DernierJeuAffiche = await ChargerEtatJeuAsync();
        configuration.DernierSuccesAffiche = await ChargerEtatSuccesAsync();
        configuration.DerniereListeSuccesAffichee = await ChargerEtatListeSuccesAsync();
        bool etatNormalise = NormaliserEtatApplication(configuration);
        bool utilisateurLegacyRecupere =
            !fichierUtilisateurExiste
            && !string.IsNullOrWhiteSpace(configuration.Pseudo)
            && !string.IsNullOrWhiteSpace(configuration.CleApiWeb);

        if (etatNormalise)
        {
            await SauvegarderEtatApplicationAsync(configuration);
        }

        if (utilisateurLegacyRecupere)
        {
            try
            {
                await SauvegarderUtilisateurAsync(configuration);
            }
            catch { }
        }

        return configuration;
    }

    /*
     * Recharge de manière synchrone la configuration minimale utile avant
     * le premier affichage, notamment la géométrie de fenêtre.
     */
    public static ConfigurationConnexion ChargerConfigurationInitialeFenetre()
    {
        FinaliserFichierTemporaireSiNecessaire(CheminFichierConfiguration);

        if (!File.Exists(CheminFichierConfiguration))
        {
            return new ConfigurationConnexion();
        }

        try
        {
            using FileStream fluxLecture = File.OpenRead(CheminFichierConfiguration);

            if (fluxLecture.Length == 0)
            {
                return new ConfigurationConnexion();
            }

            return JsonSerializer.Deserialize<ConfigurationConnexion>(fluxLecture, OptionsJson)
                ?? new ConfigurationConnexion();
        }
        catch (JsonException)
        {
            return new ConfigurationConnexion();
        }
        catch (IOException)
        {
            return new ConfigurationConnexion();
        }
        catch (UnauthorizedAccessException)
        {
            return new ConfigurationConnexion();
        }
    }

    /*
     * Sauvegarde à la fois l'identité utilisateur et l'état applicatif.
     */
    public async Task SauvegarderAsync(ConfigurationConnexion configuration)
    {
        await SauvegarderUtilisateurAsync(configuration);
        await SauvegarderEtatApplicationAsync(configuration);
    }

    /*
     * Sauvegarde uniquement les informations d'identification du compte.
     */
    public async Task SauvegarderUtilisateurAsync(ConfigurationConnexion configuration)
    {
        await _verrouUtilisateur.WaitAsync();

        try
        {
            Directory.CreateDirectory(ObtenirDossierConfiguration());
            EtatUtilisateurLocal etatUtilisateur = new()
            {
                Pseudo = configuration.Pseudo,
                CleApiWeb = configuration.CleApiWeb,
            };
            string cheminSauvegarde = CheminFichierUtilisateur + ExtensionSauvegarde;
            bool sauvegardePrecedenteDisponible = false;

            if (File.Exists(CheminFichierUtilisateur))
            {
                File.Copy(CheminFichierUtilisateur, cheminSauvegarde, overwrite: true);
                sauvegardePrecedenteDisponible = true;
            }

            try
            {
                await SauvegarderJsonAsync(CheminFichierUtilisateur, etatUtilisateur);

                EtatUtilisateurLocal? verification = await ChargerJsonAsync<EtatUtilisateurLocal>(
                    CheminFichierUtilisateur
                );

                if (!EtatUtilisateurCorrespond(verification, etatUtilisateur))
                {
                    if (sauvegardePrecedenteDisponible)
                    {
                        RestaurerSauvegardeUtilisateur(cheminSauvegarde);
                    }

                    throw new IOException(
                        "La vérification du fichier utilisateur a échoué après l'écriture."
                    );
                }
            }
            catch (Exception exception)
            {
                JournaliserIncidentPersistance(
                    "sauvegarde_utilisateur_echec",
                    $"{exception.GetType().Name}: {exception.Message}"
                );
                throw;
            }
        }
        finally
        {
            _verrouUtilisateur.Release();
        }
    }

    /*
     * Sauvegarde l'état applicatif visible, y compris les fichiers séparés
     * du dernier jeu et de la liste des succès.
     */
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

    /*
     * Recharge l'état utilisateur local depuis son fichier dédié, avec repli
     * vers l'ancien fichier de configuration si nécessaire.
     */
    private static async Task<EtatUtilisateurLocal?> ChargerEtatUtilisateurAsync()
    {
        if (File.Exists(CheminFichierUtilisateur))
        {
            EtatUtilisateurLocal? utilisateur = await ChargerJsonAsync<EtatUtilisateurLocal>(
                CheminFichierUtilisateur
            );

            if (utilisateur is not null)
            {
                return utilisateur;
            }
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

    /*
     * Recharge le dernier jeu affiché depuis son fichier dédié ou l'ancien
     * fichier de configuration lorsqu'il n'a pas encore été découpé.
     */
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

    /*
     * Recharge le dernier succès affiché depuis le stockage local dédié.
     */
    private static async Task<EtatSuccesAfficheLocal?> ChargerEtatSuccesAsync()
    {
        return File.Exists(CheminFichierSucces)
            ? await ChargerJsonAsync<EtatSuccesAfficheLocal>(CheminFichierSucces)
            : null;
    }

    /*
     * Recharge la dernière liste de succès affichée depuis le stockage local.
     */
    private static async Task<EtatListeSuccesAfficheeLocal?> ChargerEtatListeSuccesAsync()
    {
        return File.Exists(CheminFichierListeSucces)
            ? await ChargerJsonAsync<EtatListeSuccesAfficheeLocal>(CheminFichierListeSucces)
            : null;
    }

    /*
     * Retourne le dossier applicatif où sont stockées les données locales.
     */
    private static string ObtenirDossierConfiguration()
    {
        string dossierAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData
        );
        return Path.Combine(dossierAppData, "RA-Compagnon");
    }

    /*
     * Extrait proprement une valeur texte d'un objet JSON lu depuis disque.
     */
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

    /*
     * Charge un fichier JSON générique avec prise en charge du fichier
     * temporaire de secours lorsqu'il existe seul.
     */
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

    /*
     * Sauvegarde un objet JSON via un fichier temporaire pour limiter les
     * risques de corruption en cas d'interruption.
     */
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

    /*
     * Sauvegarde un objet optionnel ou supprime son fichier lorsqu'aucune
     * donnée ne doit être conservée.
     */
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

    /*
     * Détermine quel chemin lire entre le fichier final et son éventuel
     * équivalent temporaire.
     */
    private static string DeterminerCheminLecture(string cheminFichier)
    {
        return File.Exists(cheminFichier) ? cheminFichier : cheminFichier + ExtensionTemporaire;
    }

    /*
     * Compare deux états utilisateur pour confirmer qu'une sauvegarde relue
     * correspond exactement aux valeurs qui viennent d'être écrites.
     */
    private static bool EtatUtilisateurCorrespond(
        EtatUtilisateurLocal? gauche,
        EtatUtilisateurLocal droite
    )
    {
        return gauche is not null
            && string.Equals(gauche.Pseudo, droite.Pseudo, StringComparison.Ordinal)
            && string.Equals(gauche.CleApiWeb, droite.CleApiWeb, StringComparison.Ordinal);
    }

    /*
     * Restaure la dernière sauvegarde utilisateur connue lorsqu'une écriture
     * plus récente ne peut pas être vérifiée correctement.
     */
    private static void RestaurerSauvegardeUtilisateur(string cheminSauvegarde)
    {
        if (!File.Exists(cheminSauvegarde))
        {
            return;
        }

        if (File.Exists(CheminFichierUtilisateur))
        {
            File.Delete(CheminFichierUtilisateur);
        }

        File.Copy(cheminSauvegarde, CheminFichierUtilisateur, overwrite: true);
    }

    /*
     * Inscrit un incident de persistance dans un journal local dédié sans
     * dépendre du mode diagnostic global.
     */
    private static void JournaliserIncidentPersistance(string evenement, string details)
    {
        try
        {
            string cheminJournal = Path.Combine(
                ObtenirDossierConfiguration(),
                "journal-configuration-locale.log"
            );
            Directory.CreateDirectory(ObtenirDossierConfiguration());
            File.AppendAllText(
                cheminJournal,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] evenement={evenement};details={details}{Environment.NewLine}"
            );
        }
        catch { }
    }

    /*
     * Finalise un fichier temporaire si celui-ci est resté sur disque après
     * une écriture interrompue.
     */
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

    /*
     * Normalise l'état applicatif chargé en supprimant les valeurs invalides
     * et en réalignant les dépendances entre les différents fichiers.
     */
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