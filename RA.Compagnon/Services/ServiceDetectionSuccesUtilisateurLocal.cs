using RA.Compagnon.Modeles.Etat;

/*
 * Compare deux instantanés d'état utilisateur local afin d'identifier les
 * succès nouvellement débloqués entre deux lectures.
 */
namespace RA.Compagnon.Services;

/*
 * Détecte les transitions de déblocage softcore et hardcore à partir des
 * états locaux persistés du jeu utilisateur.
 */
public sealed class ServiceDetectionSuccesUtilisateurLocal
{
    /*
     * Retourne la liste des succès qui apparaissent comme nouveaux entre
     * un état précédent et l'état courant.
     */
    public static IReadOnlyList<EtatSuccesUtilisateurLocal> DetecterNouveauxSucces(
        EtatJeuUtilisateurLocal? precedent,
        EtatJeuUtilisateurLocal courant
    )
    {
        if (courant.Succes.Count == 0)
        {
            return [];
        }

        Dictionary<int, EtatSuccesUtilisateurLocal> succesPrecedents =
            precedent?.Succes.ToDictionary(item => item.AchievementId) ?? [];

        List<EtatSuccesUtilisateurLocal> nouveauxSucces = [];

        foreach (EtatSuccesUtilisateurLocal succesCourant in courant.Succes)
        {
            succesPrecedents.TryGetValue(
                succesCourant.AchievementId,
                out EtatSuccesUtilisateurLocal? succesPrecedent
            );

            bool estNouveauHardcore =
                succesCourant.EstHardcore
                && !string.Equals(
                    succesPrecedent?.DateDeblocageHardcoreUtc,
                    succesCourant.DateDeblocageHardcoreUtc,
                    StringComparison.Ordinal
                );

            bool estNouveauSoftcore =
                !estNouveauHardcore
                && succesCourant.EstDebloque
                && !string.Equals(
                    succesPrecedent?.DateDeblocageUtc,
                    succesCourant.DateDeblocageUtc,
                    StringComparison.Ordinal
                );

            if (estNouveauHardcore || estNouveauSoftcore)
            {
                nouveauxSucces.Add(succesCourant);
            }
        }

        return nouveauxSucces;
    }
}