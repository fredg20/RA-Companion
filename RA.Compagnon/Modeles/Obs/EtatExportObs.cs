/*
 * Représente l'état minimal exporté vers OBS pour les sources navigateur,
 * texte et les futurs overlays dédiés au streaming.
 */
namespace RA.Compagnon.Modeles.Obs;

/*
 * Regroupe les données publiques que Compagnon peut exposer à OBS sans
 * inclure de clé API ni d'information sensible du compte utilisateur.
 */
public sealed class EtatExportObs
{
    public int VersionSchema { get; init; } = 1;

    public string MisAJourUtc { get; init; } = DateTimeOffset.UtcNow.ToString("O");

    public JeuExportObs Jeu { get; init; } = new();

    public ProgressionExportObs Progression { get; init; } = new();

    public SuccesExportObs SuccesCourant { get; init; } = new();

    public List<SuccesBadgeExportObs> GrilleSuccesJeu { get; init; } = [];

    public SuccesDebloqueExportObs DernierSuccesObtenu { get; init; } = new();

    public string EtatSynchronisation { get; init; } = string.Empty;

    public string ModeAffichageSucces { get; init; } = string.Empty;
}

/*
 * Décrit le jeu affiché dans Compagnon avec les informations utiles à un
 * overlay, sans dépendre directement de la mise en page WPF.
 */
public sealed class JeuExportObs
{
    public int IdentifiantJeu { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string Statut { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public int IdentifiantConsole { get; init; }

    public string Genre { get; init; } = string.Empty;

    public string Developpeur { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public string ImageIcone { get; init; } = string.Empty;

    public string ImageConsole { get; init; } = string.Empty;
}

/*
 * Isole les textes et valeurs numériques de progression afin de permettre à
 * OBS d'afficher une barre, un texte court ou une source dédiée.
 */
public sealed class ProgressionExportObs
{
    public string Resume { get; init; } = string.Empty;

    public string Pourcentage { get; init; } = string.Empty;

    public double Valeur { get; init; }
}

/*
 * Décrit le succès actuellement mis en avant dans Compagnon.
 */
public sealed class SuccesExportObs
{
    public int IdentifiantJeu { get; init; }

    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DetailsPoints { get; init; } = string.Empty;

    public string DetailsFaisabilite { get; init; } = string.Empty;

    public string Badge { get; init; } = string.Empty;

    public bool EstHardcore { get; init; }
}

/*
 * Décrit un badge léger exporté pour la grille contextuelle affichée dans
 * l'overlay OBS.
 */
public sealed class SuccesBadgeExportObs
{
    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Badge { get; init; } = string.Empty;

    public bool EstDebloque { get; init; }

    public bool EstHardcore { get; init; }

    public bool EstSelectionne { get; init; }
}

/*
 * Décrit le dernier succès détecté comme obtenu pendant la session courante.
 */
public sealed class SuccesDebloqueExportObs
{
    public int IdentifiantJeu { get; init; }

    public int IdentifiantSucces { get; init; }

    public string Titre { get; init; } = string.Empty;

    public int Points { get; init; }

    public string Mode { get; init; } = string.Empty;

    public string DateObtention { get; init; } = string.Empty;
}
