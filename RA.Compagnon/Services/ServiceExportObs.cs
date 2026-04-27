using System.IO;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using RA.Compagnon.Modeles.Obs;

/*
 * Gère l'export local des données publiques de Compagnon vers des fichiers
 * consommables par OBS Studio.
 */
namespace RA.Compagnon.Services;

/*
 * Écrit un état JSON, des sources texte et un overlay HTML minimal dans un
 * dossier stable afin qu'OBS puisse les relire pendant que Compagnon tourne.
 */
public sealed class ServiceExportObs
{
    private const string ExtensionTemporaire = ".tmp";
    private const int NombreTentativesRemplacement = 4;
    private static readonly TimeSpan DelaiRepriseRemplacement = TimeSpan.FromMilliseconds(80);
    private static readonly SemaphoreSlim VerrouFichiersObs = new(1, 1);

    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string DossierExportObs =>
        Path.Combine(ServiceModeDiagnostic.DossierApplication, "OBS");

    public static string CheminEtatJson => Path.Combine(DossierExportObs, "state.json");

    public static string CheminOverlayHtml => Path.Combine(DossierExportObs, "index.html");

    public static string CheminOverlayCss => Path.Combine(DossierExportObs, "overlay.css");

    public static string CheminOverlayJs => Path.Combine(DossierExportObs, "overlay.js");

    public static string CheminOverlaySuccesEmblemePng =>
        Path.Combine(DossierExportObs, "succes-embleme.png");

    public static string CheminOverlaySuccesEmblemeHardcorePng =>
        Path.Combine(DossierExportObs, "succes-embleme-hardcore.png");

    public static string CheminLayoutJson => Path.Combine(DossierExportObs, "layout.json");

    private static string DossierModelesOverlay =>
        Path.Combine(AppContext.BaseDirectory, "ObsOverlay");

    /*
     * Publie l'état OBS complet en gardant chaque écriture atomique pour
     * éviter qu'OBS lise un fichier partiellement écrit.
     */
    public async Task ExporterAsync(EtatExportObs etat, CancellationToken jetonAnnulation = default)
    {
        await VerrouFichiersObs.WaitAsync(jetonAnnulation);

        try
        {
            Directory.CreateDirectory(DossierExportObs);
            await EcrireJsonAsync(CheminEtatJson, etat, jetonAnnulation);
            await EcrireOverlayAsync(jetonAnnulation);
            await GarantirLayoutJsonAsync(jetonAnnulation);
            await EcrireSourcesTexteAsync(etat, jetonAnnulation);
        }
        finally
        {
            VerrouFichiersObs.Release();
        }
    }

    /*
     * Persiste le layout OBS reçu du navigateur avec le même verrou et le même
     * remplacement atomique que l'export principal.
     */
    public static async Task EcrireLayoutJsonDepuisOverlayAsync(
        string contenuJson,
        CancellationToken jetonAnnulation = default
    )
    {
        using JsonDocument document = JsonDocument.Parse(contenuJson);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Le layout OBS doit être un objet JSON.");
        }

        await VerrouFichiersObs.WaitAsync(jetonAnnulation);

        try
        {
            Directory.CreateDirectory(DossierExportObs);
            await EcrireTexteAsync(CheminLayoutJson, contenuJson, jetonAnnulation);
        }
        finally
        {
            VerrouFichiersObs.Release();
        }
    }

    /*
     * Écrit les fichiers texte séparés que l'utilisateur pourra brancher dans
     * OBS avec des sources texte indépendantes.
     */
    private static async Task EcrireSourcesTexteAsync(
        EtatExportObs etat,
        CancellationToken jetonAnnulation
    )
    {
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "jeu-titre.txt"),
            NettoyerTexteSource(etat.Jeu.Titre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "jeu-statut.txt"),
            NettoyerTexteSource(etat.Jeu.Statut),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "progression.txt"),
            ConstruireProgressionTexte(etat),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-titre.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Titre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-description.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Description),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "succes-badge.txt"),
            NettoyerTexteSource(etat.SuccesCourant.Badge),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "dernier-succes.txt"),
            ConstruireDernierSuccesTexte(etat),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "synchronisation.txt"),
            NettoyerTexteSource(etat.EtatSynchronisation),
            jetonAnnulation
        );
        await EcrireSourcesTexteDetailleesAsync(etat, jetonAnnulation);
    }

    /*
     * Publie une source texte par donnée afin de permettre une composition OBS
     * fine, inspirée du fonctionnement de RA Layout Manager.
     */
    private static async Task EcrireSourcesTexteDetailleesAsync(
        EtatExportObs etat,
        CancellationToken jetonAnnulation
    )
    {
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-titre.txt"),
            NettoyerTexteSource(etat.Jeu.Titre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-statut.txt"),
            NettoyerTexteSource(etat.Jeu.Statut),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-details.txt"),
            NettoyerTexteSource(etat.Jeu.Details),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-genre.txt"),
            NettoyerTexteSource(etat.Jeu.Genre),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-developpeur.txt"),
            NettoyerTexteSource(etat.Jeu.Developpeur),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-progression.txt"),
            NettoyerTexteSource(etat.Progression.Resume),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-pourcentage.txt"),
            NettoyerTexteSource(etat.Progression.Pourcentage),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-valeur-progression.txt"),
            Math.Clamp(etat.Progression.Valeur, 0, 100).ToString("0.##"),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-mode-affichage.txt"),
            NettoyerTexteSource(etat.ModeAffichageSucces),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "game-info-synchronisation.txt"),
            NettoyerTexteSource(etat.EtatSynchronisation),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-lastGameID.txt"),
            etat.UserInfo.LastGameId.ToString(CultureInfo.InvariantCulture),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-totalPoints.txt"),
            etat.UserInfo.TotalPoints.ToString(CultureInfo.InvariantCulture),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-totalTruePoints.txt"),
            etat.UserInfo.TotalTruePoints.ToString(CultureInfo.InvariantCulture),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-rank.txt"),
            etat.UserInfo.Rank.ToString(CultureInfo.InvariantCulture),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-awards.txt"),
            etat.UserInfo.Awards.ToString(CultureInfo.InvariantCulture),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-userPic.txt"),
            NettoyerTexteSource(etat.UserInfo.UserPic),
            jetonAnnulation
        );
        await EcrireTexteAsync(
            Path.Combine(DossierExportObs, "user-info-retroRatio.txt"),
            NettoyerTexteSource(etat.UserInfo.RetroRatio),
            jetonAnnulation
        );
        SupprimerSourcesTexteObsoletes();
    }

    /*
     * Nettoie les anciens fichiers texte devenus inutiles afin que le dossier
     * OBS ne conserve pas de sources supprimées dans les versions récentes.
     */
    private static void SupprimerSourcesTexteObsoletes()
    {
        string[] fichiers =
        [
            "game-info-id-jeu.txt",
            "game-info-id-console.txt",
            "user-info-succes-id.txt",
            "user-info-succes-titre.txt",
            "user-info-succes-description.txt",
            "user-info-succes-points.txt",
            "user-info-succes-faisabilite.txt",
            "user-info-succes-hardcore.txt",
            "user-info-succes-badge.txt",
            "user-info-dernier-succes.txt",
            "user-info-dernier-mode.txt",
            "user-info-dernier-points.txt",
            "user-info-dernier-date.txt",
            "user-info-badges-grille.txt",
        ];

        foreach (string fichier in fichiers)
        {
            string chemin = Path.Combine(DossierExportObs, fichier);

            try
            {
                if (File.Exists(chemin))
                {
                    File.Delete(chemin);
                }
            }
            catch
            {
            }
        }
    }

    /*
     * Sérialise l'état principal au format JSON lisible par une source
     * navigateur ou par un outil externe.
     */
    private static async Task EcrireJsonAsync<T>(
        string chemin,
        T donnees,
        CancellationToken jetonAnnulation
    )
    {
        string json = JsonSerializer.Serialize(donnees, OptionsJson);
        await EcrireTexteAsync(chemin, json, jetonAnnulation);
    }

    /*
     * Écrit un fichier texte par remplacement atomique via un fichier
     * temporaire dans le même dossier.
     */
    private static async Task EcrireTexteAsync(
        string chemin,
        string contenu,
        CancellationToken jetonAnnulation
    )
    {
        string cheminTemporaire = chemin + ExtensionTemporaire;
        await File.WriteAllTextAsync(cheminTemporaire, contenu, jetonAnnulation);
        await RemplacerFichierAvecRepriseAsync(cheminTemporaire, chemin, jetonAnnulation);
    }

    /*
     * Copie les modèles HTML, CSS et JS de l'overlay dans le dossier OBS afin
     * de garder une structure lisible et modifiable en dehors du code C#.
     */
    private static async Task EcrireOverlayAsync(CancellationToken jetonAnnulation)
    {
        await EcrireTexteAsync(CheminOverlayHtml, LireModeleOverlay("index.html"), jetonAnnulation);
        await EcrireTexteAsync(
            CheminOverlayCss,
            CompilerScssOverlayVersCss(
                LireModeleOverlay("normalize.scss"),
                LireModeleOverlay("overlay.scss")
            ),
            jetonAnnulation
        );
        await EcrireTexteAsync(CheminOverlayJs, LireModeleOverlay("overlay.js"), jetonAnnulation);
        await EcrireOctetsAsync(
            CheminOverlaySuccesEmblemePng,
            LireModeleOverlayBinaire("succes-embleme.png"),
            jetonAnnulation
        );
        await EcrireOctetsAsync(
            CheminOverlaySuccesEmblemeHardcorePng,
            LireModeleOverlayBinaire("succes-embleme-hardcore.png"),
            jetonAnnulation
        );
    }

    /*
     * Garantit la présence d'un fichier layout.json afin que l'overlay puisse
     * charger une disposition persistée même avant la première édition.
     */
    private static async Task GarantirLayoutJsonAsync(CancellationToken jetonAnnulation)
    {
        if (File.Exists(CheminLayoutJson))
        {
            return;
        }

        await EcrireTexteAsync(CheminLayoutJson, "{}", jetonAnnulation);
    }

    /*
     * Lit un fichier source de l'overlay embarqué à côté de l'application afin
     * de pouvoir le réécrire tel quel dans le dossier OBS.
     */
    private static string LireModeleOverlay(string nomFichier)
    {
        string cheminModele = Path.Combine(DossierModelesOverlay, nomFichier);

        if (!File.Exists(cheminModele))
        {
            throw new FileNotFoundException(
                $"Le modèle OBS '{nomFichier}' est introuvable.",
                cheminModele
            );
        }

        return File.ReadAllText(cheminModele);
    }

    /*
     * Lit un asset binaire de l'overlay, comme une image PNG, pour le recopier
     * tel quel dans le dossier OBS.
     */
    private static byte[] LireModeleOverlayBinaire(string nomFichier)
    {
        string cheminModele = Path.Combine(DossierModelesOverlay, nomFichier);

        if (!File.Exists(cheminModele))
        {
            throw new FileNotFoundException(
                $"Le modèle OBS binaire '{nomFichier}' est introuvable.",
                cheminModele
            );
        }

        return File.ReadAllBytes(cheminModele);
    }

    /*
     * Écrit un fichier binaire par remplacement atomique via un fichier
     * temporaire dans le même dossier.
     */
    private static async Task EcrireOctetsAsync(
        string chemin,
        byte[] contenu,
        CancellationToken jetonAnnulation
    )
    {
        string cheminTemporaire = chemin + ExtensionTemporaire;
        await File.WriteAllBytesAsync(cheminTemporaire, contenu, jetonAnnulation);
        await RemplacerFichierAvecRepriseAsync(cheminTemporaire, chemin, jetonAnnulation);
    }

    /*
     * Remplace un fichier avec quelques reprises courtes pour absorber les
     * lectures simultanées d'OBS, d'un navigateur ou d'un antivirus.
     */
    private static async Task RemplacerFichierAvecRepriseAsync(
        string cheminTemporaire,
        string cheminDestination,
        CancellationToken jetonAnnulation
    )
    {
        for (int tentative = 1; tentative <= NombreTentativesRemplacement; tentative++)
        {
            try
            {
                File.Move(cheminTemporaire, cheminDestination, overwrite: true);
                return;
            }
            catch (IOException) when (tentative < NombreTentativesRemplacement)
            {
                await Task.Delay(DelaiRepriseRemplacement, jetonAnnulation);
            }
            catch (UnauthorizedAccessException) when (tentative < NombreTentativesRemplacement)
            {
                await Task.Delay(DelaiRepriseRemplacement, jetonAnnulation);
            }
        }

        File.Move(cheminTemporaire, cheminDestination, overwrite: true);
    }

    /*
     * Utilise le fichier SCSS comme source unique du style OBS, puis produit
     * le CSS servi au navigateur. Le style actuel reste volontairement dans un
     * sous-ensemble SCSS directement compatible avec le CSS final.
     */
    private static string CompilerScssOverlayVersCss(
        string contenuNormalizeScss,
        string contenuOverlayScss
    )
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            contenuNormalizeScss.Trim(),
            contenuOverlayScss.Trim()
        );
    }

    /*
     * Construit une version compacte de la progression utilisable directement
     * comme source texte OBS.
     */
    private static string ConstruireProgressionTexte(EtatExportObs etat)
    {
        string resume = NettoyerTexteSource(etat.Progression.Resume);
        string pourcentage = NettoyerTexteSource(etat.Progression.Pourcentage);

        if (string.IsNullOrWhiteSpace(resume))
        {
            return pourcentage;
        }

        if (string.IsNullOrWhiteSpace(pourcentage))
        {
            return resume;
        }

        return $"{resume} - {pourcentage}";
    }

    /*
     * Formate le dernier succès obtenu de manière courte pour les overlays ou
     * les sources texte dédiées aux alertes.
     */
    private static string ConstruireDernierSuccesTexte(EtatExportObs etat)
    {
        SuccesDebloqueExportObs succes = etat.DernierSuccesObtenu;

        if (succes.IdentifiantSucces <= 0 || string.IsNullOrWhiteSpace(succes.Titre))
        {
            return string.Empty;
        }

        string mode = string.IsNullOrWhiteSpace(succes.Mode) ? "succès" : succes.Mode;
        return $"{succes.Titre} - {succes.Points} pts - {mode}";
    }

    /*
     * Réduit les retours de ligne et espaces multiples pour rendre les sources
     * texte plus prévisibles dans OBS.
     */
    private static string NettoyerTexteSource(string? valeur)
    {
        return string.IsNullOrWhiteSpace(valeur)
            ? string.Empty
            : string.Join(' ', valeur.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
