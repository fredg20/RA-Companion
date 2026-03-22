# API RetroAchievements

## But

Ce fichier sert de mémoire locale du site officiel de documentation de l'API RetroAchievements :

- https://api-docs.retroachievements.org/

Il ne remplace pas la documentation officielle, mais il centralise les règles importantes et le catalogue des endpoints pour le projet.

## Règles générales

- L'API RetroAchievements est une API HTTP JSON.
- Les endpoints documentés sont appelés via `GET`.
- Les routes réelles sont exposées sous la forme `https://retroachievements.org/API/API_*.php`.
- L'authentification se fait avec la clé Web API du compte via le paramètre de query string `y`.
- Le rate limiting existe. La documentation ne publie pas de quota fixe public.
- La doc recommande d'être raisonnable sur la fréquence des appels et de mettre en cache les données stables.
- Depuis 2025, le nom d'utilisateur n'est plus une valeur stable. Un utilisateur peut changer de pseudo.
- Quand un endpoint accepte `username` ou `ULID`, il vaut mieux résoudre d'abord le pseudo puis conserver le `ULID`.
- Les bibliothèques clientes officielles mentionnées par la doc sont JavaScript/TypeScript et Kotlin.
- Il n'y a pas de client C# officiel documenté sur le site.

## Points importants pour notre appli

- Pour un compagnon temps réel, les endpoints les plus utiles sont `User Profile`, `User Unlocks (by date range)`, `User Game Progress`, `Game Extended Details` et `User Recently Played Games`.
- `User Summary` est documenté comme lent et sujet à l'over-fetching.
- `User Completed Games` est documenté comme legacy.
- `All System Games and Hashes` doit être agressivement mis en cache car la réponse peut être très volumineuse.

## Catalogue complet des endpoints

### Démarrage

- Welcome : https://api-docs.retroachievements.org/
- Getting Started : https://api-docs.retroachievements.org/getting-started.html

### User

- User Profile
  - Doc : https://api-docs.retroachievements.org/v1/get-user-profile.html
  - Route : `API_GetUserProfile.php`
- User Unlocks (most recent)
  - Doc : https://api-docs.retroachievements.org/v1/get-user-recent-achievements.html
  - Route : `API_GetUserRecentAchievements.php`
- User Unlocks (by date range)
  - Doc : https://api-docs.retroachievements.org/v1/get-achievements-earned-between.html
  - Route : `API_GetAchievementsEarnedBetween.php`
- User Unlocks (on date)
  - Doc : https://api-docs.retroachievements.org/v1/get-achievements-earned-on-day.html
  - Route : `API_GetAchievementsEarnedOnDay.php`
- User Game Progress
  - Doc : https://api-docs.retroachievements.org/v1/get-game-info-and-user-progress.html
  - Route : `API_GetGameInfoAndUserProgress.php`
- User Completion Progress
  - Doc : https://api-docs.retroachievements.org/v1/get-user-completion-progress.html
  - Route : `API_GetUserCompletionProgress.php`
- User Awards / Badges
  - Doc : https://api-docs.retroachievements.org/v1/get-user-awards.html
  - Route : `API_GetUserAwards.php`
- User Claims
  - Doc : https://api-docs.retroachievements.org/v1/get-user-claims.html
  - Route : `API_GetUserClaims.php`
- Game Rank and Score
  - Doc : https://api-docs.retroachievements.org/v1/get-user-game-rank-and-score.html
  - Route : `API_GetUserGameRankAndScore.php`
- User Points
  - Doc : https://api-docs.retroachievements.org/v1/get-user-points.html
  - Route : `API_GetUserPoints.php`
- User Specific Games Progress
  - Doc : https://api-docs.retroachievements.org/v1/get-user-progress.html
  - Route : `API_GetUserProgress.php`
- User Recently Played Games
  - Doc : https://api-docs.retroachievements.org/v1/get-user-recently-played-games.html
  - Route : `API_GetUserRecentlyPlayedGames.php`
- User Summary
  - Doc : https://api-docs.retroachievements.org/v1/get-user-summary.html
  - Route : `API_GetUserSummary.php`
  - Note : lent et souvent trop large pour un usage ciblé.
- User Completed Games
  - Doc : https://api-docs.retroachievements.org/v1/get-user-completed-games.html
  - Route : `API_GetUserCompletedGames.php`
  - Note : legacy.
- User Want to Play Games List
  - Doc : https://api-docs.retroachievements.org/v1/get-user-want-to-play-list.html
  - Route : `API_GetUserWantToPlayList.php`
- Users I Follow
  - Doc : https://api-docs.retroachievements.org/v1/get-users-i-follow.html
  - Route : `API_GetUsersIFollow.php`
- Users Following Me
  - Doc : https://api-docs.retroachievements.org/v1/get-users-following-me.html
  - Route : `API_GetUsersFollowingMe.php`
- User Set Requests
  - Doc : https://api-docs.retroachievements.org/v1/get-user-set-requests.html
  - Route : `API_GetUserSetRequests.php`

### Game

- Game Summary
  - Doc : https://api-docs.retroachievements.org/v1/get-game.html
  - Route : `API_GetGame.php`
- Game Extended Details
  - Doc : https://api-docs.retroachievements.org/v1/get-game-extended.html
  - Route : `API_GetGameExtended.php`
- Supported Game Files
  - Doc : https://api-docs.retroachievements.org/v1/get-game-hashes.html
  - Route : `API_GetGameHashes.php`
- Game Achievement IDs
  - Doc : https://api-docs.retroachievements.org/v1/get-achievement-count.html
  - Route : `API_GetAchievementCount.php`
- Game Unlocks Distribution
  - Doc : https://api-docs.retroachievements.org/v1/get-achievement-distribution.html
  - Route : `API_GetAchievementDistribution.php`
- High Scores
  - Doc : https://api-docs.retroachievements.org/v1/get-game-rank-and-score.html
  - Route : `API_GetGameRankAndScore.php`
- Game Progression
  - Doc : https://api-docs.retroachievements.org/v1/get-game-progression.html
  - Route : `API_GetGameProgression.php`

### Leaderboards

- Game Leaderboards
  - Doc : https://api-docs.retroachievements.org/v1/get-game-leaderboards.html
  - Route : `API_GetGameLeaderboards.php`
- Leaderboard Entries
  - Doc : https://api-docs.retroachievements.org/v1/get-leaderboard-entries.html
  - Route : `API_GetLeaderboardEntries.php`
- User Game Leaderboards
  - Doc : https://api-docs.retroachievements.org/v1/get-user-game-leaderboards.html
  - Route : `API_GetUserGameLeaderboards.php`

### System

- Get All Systems
  - Doc : https://api-docs.retroachievements.org/v1/get-console-ids.html
  - Route : `API_GetConsoleIDs.php`
- All System Games and Hashes
  - Doc : https://api-docs.retroachievements.org/v1/get-game-list.html
  - Route : `API_GetGameList.php`
  - Note : à mettre fortement en cache.

### Achievement

- Achievement Unlocks
  - Doc : https://api-docs.retroachievements.org/v1/get-achievement-unlocks.html
  - Route : `API_GetAchievementUnlocks.php`

### Comment

- Comments
  - Doc : https://api-docs.retroachievements.org/v1/get-comments.html
  - Route : `API_GetComments.php`

### Feed

- All Recent Game Awards
  - Doc : https://api-docs.retroachievements.org/v1/get-recent-game-awards.html
  - Route : `API_GetRecentGameAwards.php`
- Active Claims
  - Doc : https://api-docs.retroachievements.org/v1/get-active-claims.html
  - Route : `API_GetActiveClaims.php`
- Inactive Claims
  - Doc : https://api-docs.retroachievements.org/v1/get-claims.html
  - Route : `API_GetClaims.php`
- Top Ten Ranked Users
  - Doc : https://api-docs.retroachievements.org/v1/get-top-ten-users.html
  - Route : `API_GetTopTenUsers.php`

### Event

- Achievement of the Week
  - Doc : https://api-docs.retroachievements.org/v1/get-achievement-of-the-week.html
  - Route : `API_GetAchievementOfTheWeek.php`

### Ticket

- Get Ticket by ID
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-ticket-by-id.html
  - Route : `API_GetTicketData.php`
  - Mode : ciblage par identifiant de ticket.
- Get Most Ticketed Games
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-most-ticketed-games.html
  - Route : `API_GetTicketData.php`
  - Mode : `f=1`
- Get Most Recent Tickets
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-most-recent-tickets.html
  - Route : `API_GetTicketData.php`
  - Mode : pagination via `o` et `c`
- Get Game Ticket Stats
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-game-ticket-stats.html
  - Route : `API_GetTicketData.php`
  - Mode : ciblage par jeu
- Get Developer Ticket Stats
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-developer-ticket-stats.html
  - Route : `API_GetTicketData.php`
  - Mode : ciblage par développeur
- Get Achievement Ticket Stats
  - Doc : https://api-docs.retroachievements.org/v1/get-ticket-data/get-achievement-ticket-stats.html
  - Route : `API_GetTicketData.php`
  - Mode : ciblage par achievement

## Notes de modélisation C#

- Prévoir des DTO dédiés par endpoint, car les formes de réponse changent souvent d'une route à l'autre.
- Prévoir un parsing de dates tolérant.
- Isoler la clé API dans une configuration locale non versionnée.
- Prévoir du cache local pour les métadonnées stables :
  - systèmes
  - jeux
  - hashes
  - données étendues d'un jeu
- Prévoir un polling raisonnable au lieu de sur-solliciter les endpoints.

## Sources officielles utilisées

- Welcome : https://api-docs.retroachievements.org/
- Getting Started : https://api-docs.retroachievements.org/getting-started.html
- User Profile : https://api-docs.retroachievements.org/v1/get-user-profile.html
- User Unlocks (by date range) : https://api-docs.retroachievements.org/v1/get-achievements-earned-between.html
- User Summary : https://api-docs.retroachievements.org/v1/get-user-summary.html
- User Completed Games : https://api-docs.retroachievements.org/v1/get-user-completed-games.html
- User Specific Games Progress : https://api-docs.retroachievements.org/v1/get-user-progress.html
- All System Games and Hashes : https://api-docs.retroachievements.org/v1/get-game-list.html
