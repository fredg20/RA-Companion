using RA.Compagnon.Modeles.Etat;

namespace RA.Compagnon.Services;

public sealed class ServiceDetectionSuccesUtilisateurLocal
{
    public IReadOnlyList<EtatSuccesUtilisateurLocal> DetecterNouveauxSucces(
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
