namespace RA.Compagnon.Modeles.Presentation;

/// <summary>
/// Représente une ligne d'information prête à être affichée dans l'interface.
/// </summary>
public sealed class LigneInformationAffichee
{
    public required string Libelle { get; init; }

    public required string Valeur { get; init; }
}
