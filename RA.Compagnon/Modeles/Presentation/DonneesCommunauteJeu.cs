using RA.Compagnon.Modeles.Api.V2.User;

/*
 * Regroupe les données brutes de communauté utilisées pour décrire l'état
 * des claims autour d'un jeu.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte les claims actifs et ceux de l'utilisateur pour un jeu donné.
 */
public sealed class DonneesCommunauteJeu
{
    public IReadOnlyList<UserClaimV2> ClaimsActivesJeu { get; init; } = [];

    public IReadOnlyList<UserClaimV2> ClaimsUtilisateurJeu { get; init; } = [];
}