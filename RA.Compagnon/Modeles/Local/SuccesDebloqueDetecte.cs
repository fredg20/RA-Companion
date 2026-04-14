/*
 * Représente un succès nouvellement détecté comme débloqué, qu'il provienne
 * d'une source locale ou d'une détection API.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Transporte les informations essentielles d'un succès détecté pendant une
 * session de jeu.
 */
public sealed class SuccesDebloqueDetecte
{
    public int IdentifiantJeu { get; init; }

    public string TitreJeu { get; init; } = string.Empty;

    public int IdentifiantSucces { get; init; }

    public string TitreSucces { get; init; } = string.Empty;

    public int Points { get; init; }

    public bool Hardcore { get; init; }

    public string DateObtention { get; init; } = string.Empty;
}
