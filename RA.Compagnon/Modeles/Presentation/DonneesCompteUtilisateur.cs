using RA.Compagnon.Modeles.Api.V2.User;

/*
 * Regroupe les différentes réponses API liées au compte utilisateur avant
 * leur transformation en modèles de présentation.
 */
namespace RA.Compagnon.Modeles.Presentation;

/*
 * Transporte les données brutes de profil, de résumé, de points, de
 * récompenses et de progression utilisateur.
 */
public sealed class DonneesCompteUtilisateur
{
    public UserProfileV2? Profil { get; init; }

    public UserSummaryV2? Resume { get; init; }

    public UserPointsV2? Points { get; init; }

    public UserAwardsResponseV2? Recompenses { get; init; }

    public UserProgressResponseV2? Progression { get; init; }
}
