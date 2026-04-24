using System.IO;
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
    private readonly SemaphoreSlim _verrouExport = new(1, 1);

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

    public static string CheminLayoutJson => Path.Combine(DossierExportObs, "layout.json");

    private static string DossierModelesOverlay =>
        Path.Combine(AppContext.BaseDirectory, "ObsOverlay");

    /*
     * Publie l'état OBS complet en gardant chaque écriture atomique pour
     * éviter qu'OBS lise un fichier partiellement écrit.
     */
    public async Task ExporterAsync(EtatExportObs etat, CancellationToken jetonAnnulation = default)
    {
        await _verrouExport.WaitAsync(jetonAnnulation);

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
            _verrouExport.Release();
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
        File.Move(cheminTemporaire, chemin, overwrite: true);
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
