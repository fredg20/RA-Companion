using RA.Compagnon.Modeles.Api.V2.User;

namespace RA.Compagnon.Modeles.Presentation;

public sealed class DonneesCommunauteJeu
{
    public IReadOnlyList<UserClaimV2> ClaimsActivesJeu { get; init; } = [];

    public IReadOnlyList<UserClaimV2> ClaimsUtilisateurJeu { get; init; } = [];
}
