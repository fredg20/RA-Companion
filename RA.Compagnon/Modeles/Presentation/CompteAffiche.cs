namespace RA.Compagnon.Modeles.Presentation;

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
