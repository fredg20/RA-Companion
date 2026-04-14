/*
 * Représente la réponse de progression utilisateur par jeu dans l'API
 * RetroAchievements v2.
 */
namespace RA.Compagnon.Modeles.Api.V2.User;

/*
 * Sert de conteneur dictionnaire pour la progression utilisateur indexée
 * par identifiant de jeu.
 */
public sealed class UserProgressResponseV2 : Dictionary<string, UserGameProgressEntryV2> { }
