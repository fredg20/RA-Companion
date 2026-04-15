/*
 * Représente les informations locales minimales d'authentification de
 * l'utilisateur.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke le pseudo et la clé API Web persistés localement.
 */
public sealed class EtatUtilisateurLocal
{
    public string Pseudo { get; set; } = string.Empty;

    public string CleApiWeb { get; set; } = string.Empty;
}