/*
 * Représente l'état local d'un succès utilisateur observé dans le cache de
 * progression.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Stocke le statut débloqué ou hardcore d'un succès ainsi que ses dates de
 * déblocage connues.
 */
public sealed class EtatSuccesUtilisateurLocal
{
    public int AchievementId { get; set; }

    public bool EstDebloque { get; set; }

    public bool EstHardcore { get; set; }

    public string DateDeblocageUtc { get; set; } = string.Empty;

    public string DateDeblocageHardcoreUtc { get; set; } = string.Empty;
}
