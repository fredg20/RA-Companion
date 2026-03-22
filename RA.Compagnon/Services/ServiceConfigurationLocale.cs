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

    /// <summary>
    /// Chemin complet du fichier de configuration.
    /// </summary>
    public string CheminFichierConfiguration =>
        Path.Combine(ObtenirDossierConfiguration(), "configuration.json");

    /// <summary>
    /// Charge la configuration locale si elle existe.
    /// </summary>
    public async Task<ConfigurationConnexion> ChargerAsync()
    {
        if (!File.Exists(CheminFichierConfiguration))
        {
            return new ConfigurationConnexion();
        }

        await using FileStream fluxLecture = File.OpenRead(CheminFichierConfiguration);
        ConfigurationConnexion? configuration =
            await JsonSerializer.DeserializeAsync<ConfigurationConnexion>(fluxLecture, OptionsJson);

        return configuration ?? new ConfigurationConnexion();
    }

    /// <summary>
    /// Sauvegarde la configuration locale sur disque.
    /// </summary>
    public async Task SauvegarderAsync(ConfigurationConnexion configuration)
    {
        Directory.CreateDirectory(ObtenirDossierConfiguration());

        await using FileStream fluxEcriture = File.Create(CheminFichierConfiguration);
        await JsonSerializer.SerializeAsync(fluxEcriture, configuration, OptionsJson);
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
}
