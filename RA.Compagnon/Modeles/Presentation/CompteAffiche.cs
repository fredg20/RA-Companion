/*
 * Décrit le modèle de présentation complet du compte utilisateur tel qu'il
 * est affiché dans l'interface principale.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Regroupe les textes, sections et jeux récents déjà formatés pour la carte
 * de compte utilisateur.
 */
public sealed class CompteAffiche
{
    public string NomUtilisateur { get; init; } = string.Empty;

    public string Titre { get; init; } = string.Empty;

    public string Statut { get; init; } = string.Empty;

    public string SousStatut { get; init; } = string.Empty;

    public string Devise { get; init; } = string.Empty;

    public string Introduction { get; init; } = string.Empty;

    public string UrlAvatar { get; init; } = string.Empty;

    public IReadOnlyList<SectionInformationsAffichee> Sections { get; init; } = [];

    public IReadOnlyList<JeuRecentAffiche> JeuxRecemmentJoues { get; init; } = [];
}