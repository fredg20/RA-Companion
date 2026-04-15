/*
 * Représente une distribution de déblocages par clé/valeur telle que fournie
 * par l'API RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.Game;

/*
 * Sert de conteneur dictionnaire pour les distributions de déblocage d'un jeu.
 */
public sealed class GameUnlockDistributionV2 : Dictionary<string, int> { }