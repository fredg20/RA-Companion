using System.Globalization;
using RA.Compagnon.Modeles.Presentation;

/*
 * Convertit les informations communautaires d'un jeu en un résumé court
 * destiné à la carte visible dans l'interface.
 */
namespace RA.Compagnon.Services;

/*
 * Construit le texte synthétique lié aux claims actifs et personnels du jeu.
 */
public sealed class ServicePresentationCommunaute
{
    /*
     * Génère le résumé affiché pour la communauté du jeu à partir des claims
     * disponibles dans les données chargées.
     */
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
