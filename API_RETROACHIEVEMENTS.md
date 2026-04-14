# API RetroAchievements

## But du document

Ce fichier résume les endpoints RetroAchievements actuellement utilisés par `RA-Compagnon`.

Il ne s'agit pas d'une documentation officielle exhaustive de l'API.
Il sert surtout de référence rapide pour la maintenance du client `RA.Compagnon/Services/ClientRetroAchievements.cs`.

## Authentification

Le projet utilise la clé Web API RetroAchievements transmise via le paramètre `y`.

La plupart des appels utilisateur utilisent aussi :

- `u` pour le pseudo
- `g` pour l'identifiant de jeu
- `i` pour certains identifiants de jeu ou de console

## Endpoints utilisateur

- `API_GetUserProfile.php`
  - profil minimal de l'utilisateur
  - utilisé pour récupérer le dernier jeu et la présence riche

- `API_GetUserSummary.php`
  - résumé général du compte
  - utilisé pour les blocs de synthèse du compte

- `API_GetUserPoints.php`
  - points utilisateur

- `API_GetUserAwards.php`
  - récompenses utilisateur

- `API_GetUserProgress.php`
  - progression utilisateur sur une liste de jeux

- `API_GetUserClaims.php`
  - claims utilisateur

- `API_GetUserSetRequests.php`
  - demandes de sets utilisateur

- `API_GetUserGameRankAndScore.php`
  - rang et score utilisateur sur un jeu

- `API_GetUserRecentlyPlayedGames.php`
  - jeux récemment joués

- `API_GetAchievementsEarnedBetween.php`
  - succès obtenus entre deux horodatages

## Endpoints jeu

- `API_GetGameInfoAndUserProgress.php`
  - endpoint principal pour charger le jeu et la progression utilisateur
  - utilisé au cœur de l'affichage du jeu courant

- `API_GetGame.php`
  - détails de base d'un jeu

- `API_GetGameExtended.php`
  - détails enrichis d'un jeu

- `API_GetGameProgression.php`
  - données de progression d'un jeu
  - peut être appelé avec ou sans préférence `hardcore`

- `API_GetAchievementDistribution.php`
  - distribution de déblocage des succès d'un jeu
  - utilisé pour l'estimation de faisabilité

- `API_GetRecentGameAwards.php`
  - récompenses récentes liées aux jeux

- `API_GetGameHashes.php`
  - hashes connus pour un jeu

- `API_GetGameList.php`
  - liste des jeux d'une console
  - utilisée aussi pour les catalogues locaux

## Endpoints claims et catalogues

- `API_GetActiveClaims.php`
  - claims actifs

- `API_GetClaims.php`
  - historique ou catégorie de claims selon le paramètre utilisé

- `API_GetTopTenUsers.php`
  - top utilisateurs

- `API_GetConsoleIDs.php`
  - liste des consoles

## Points d'attention

- `API_GetGameInfoAndUserProgress.php` reste l'appel le plus critique pour l'application.
- Les endpoints de progression et de distribution peuvent être plus coûteux ; ils doivent rester ciblés.
- Le client local fusionne parfois des données API et des données locales pour éviter des régressions visuelles au rechargement.
- L'application ne propose pas aujourd'hui de reset de progression via l'API.

## Fichier source à relire

- `RA.Compagnon/Services/ClientRetroAchievements.cs`
