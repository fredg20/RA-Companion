using RA.Compagnon.Modeles.Api.V2.User;

namespace RA.Compagnon.Modeles.Presentation;

public sealed class DonneesCompteUtilisateur
{
    public UserProfileV2? Profil { get; init; }

    public UserSummaryV2? Resume { get; init; }

    public UserPointsV2? Points { get; init; }

    public UserAwardsResponseV2? Recompenses { get; init; }

    public UserProgressResponseV2? Progression { get; init; }
}
