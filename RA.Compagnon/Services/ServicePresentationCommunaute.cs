using System.Globalization;
using RA.Compagnon.Modeles.Presentation;

namespace RA.Compagnon.Services;

/// <summary>
/// Transforme les données communautaires d'un jeu en contenu prêt à afficher.
/// </summary>
public sealed class ServicePresentationCommunaute
{
    public static CommunauteJeuAffichee Construire(DonneesCommunauteJeu communaute)
    {
        List<string> segments = [];

        if (communaute.ClaimsActivesJeu.Count > 0)
        {
            segments.Add(
                communaute.ClaimsActivesJeu.Count == 1
                    ? "1 claim active"
                    : $"{communaute.ClaimsActivesJeu.Count.ToString(CultureInfo.CurrentCulture)} claims actives"
            );
        }

        if (communaute.ClaimsUtilisateurJeu.Count > 0)
        {
            segments.Add(
                communaute.ClaimsUtilisateurJeu.Count == 1
                    ? "1 claim utilisateur"
                    : $"{communaute.ClaimsUtilisateurJeu.Count.ToString(CultureInfo.CurrentCulture)} claims utilisateur"
            );
        }

        return new CommunauteJeuAffichee { Resume = string.Join(" • ", segments) };
    }
}
