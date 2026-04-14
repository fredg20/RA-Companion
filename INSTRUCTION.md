# Manuel d'utilisation de Compagnon

## 1. À quoi sert Compagnon

`Compagnon` est une application Windows conçue pour suivre un compte RetroAchievements de façon plus confortable.

Elle permet notamment de :

- voir le dernier jeu joué
- consulter la progression du jeu courant
- mettre en avant un rétrosuccès
- parcourir la grille complète des rétrosuccès
- recharger les données du jeu courant
- relancer un jeu avec `Rejouer` lorsque le contexte local le permet
- suivre certains changements de jeu détectés localement par émulateur

## 2. Premier démarrage

Au premier lancement :

1. ouvre `Compagnon`
2. clique sur le bouton `Connexion` ou `Profil`
3. renseigne ton pseudo RetroAchievements
4. renseigne ta clé Web API RetroAchievements
5. valide la connexion

Une fois la connexion réussie, `Compagnon` charge :

- ton compte
- le dernier jeu détecté
- la progression du jeu
- les rétrosuccès récents

Remarque :

- lors de la toute première utilisation, le bouton `Aide` peut être mis en avant visuellement pour guider la découverte de l'application

## 3. Organisation de la fenêtre

L'interface principale est organisée en plusieurs zones.

### En-tête

L'en-tête affiche notamment :

- l'état du compte
- le bouton `Recharger`
- le bouton `Profil`

### Jeu en cours

Cette zone affiche :

- le visuel du jeu
- le titre du jeu
- les informations principales sous forme de capsules
- le pourcentage de progression
- le résumé de progression
- le bouton `Rejouer`
- le bouton `Détails`

### Rétrosuccès en cours

Cette zone met en avant un rétrosuccès.

Elle affiche :

- le badge
- le titre
- la description
- les points, les rétropoints et l'information de mode `Softcore` ou `Hardcore` si le succès est débloqué
- l'indication de faisabilité
- les boutons de navigation
- le bouton `Passer`

### Rétrosuccès du jeu

Cette zone affiche la grille complète des badges du jeu courant.

Elle contient aussi :

- la légende visuelle `Softcore` / `Hardcore`
- le bouton de changement de mode d'affichage de la grille

## 4. Boutons principaux

### `Recharger`

Recharge les informations du jeu courant et relance une synchronisation.

À utiliser si :

- le jeu a changé
- l'affichage semble en retard
- un succès vient d'être obtenu et tu veux forcer la mise à jour

### `Rejouer`

Relance le dernier jeu local connu si `Compagnon` dispose du bon contexte.

Le bouton peut être :

- visible et actif
- visible mais grisé si le jeu est déjà `En jeu`
- masqué si aucun contexte de relance fiable n'est disponible

### `Détails`

Affiche une vue détaillée du jeu courant.

### `Passer`

Repousse localement le rétrosuccès actuellement mis en avant pour afficher le suivant.

Le succès n'est ni supprimé ni modifié sur RetroAchievements.
Il est seulement déplacé localement dans l'ordre d'affichage de la grille des succès non débloqués.

### `Aide`

Ouvre la notice d'aide intégrée.

Elle permet notamment de :

- relire le fonctionnement général de l'application
- consulter les remarques de détection locale
- retrouver des indications utiles sur certains journaux ou emplacements d'émulateurs

## 5. Utiliser la grille des rétrosuccès

La grille des rétrosuccès permet de parcourir rapidement tous les badges du jeu.

Comportement actuel :

- un clic gauche épingle un succès non débloqué dans la carte principale
- un clic droit affiche temporairement un succès dans la carte principale
- après un affichage temporaire, `Compagnon` revient ensuite au premier succès non débloqué selon le contexte

Infobulles :

- un succès non débloqué affiche simplement son titre
- un succès débloqué en `Softcore` affiche son titre puis `Softcore`
- un succès débloqué en `Hardcore` affiche son titre puis `Hardcore`

Style visuel :

- les succès `Hardcore` utilisent un contour doré avec un halo léger
- ce style apparaît dans la grille et dans la section `Rétrosuccès en cours`

## 6. Synchronisation et états

`Compagnon` peut afficher des états comme :

- `En jeu`
- `Dernier jeu`
- `Synchronisation...`

La synchronisation peut être déclenchée :

- au chargement initial
- au clic sur `Recharger`
- lors d'un changement d'état du jeu
- après certaines détections locales d'émulateur ou de succès

## 7. Softcore et Hardcore

Un rétrosuccès peut être détecté comme :

- `Softcore`
- `Hardcore`

Règle utilisée :

- si `DateEarnedHardcore` existe, le succès est considéré `Hardcore`
- sinon, si `DateEarned` existe, il est considéré `Softcore`

## 8. Détection locale des émulateurs

`Compagnon` peut accélérer l'affichage du jeu courant grâce à une détection locale.

Quand un émulateur validé est reconnu, l'application peut :

- détecter le jeu courant plus vite
- afficher `En jeu`
- relancer un jeu avec `Rejouer`
- détecter certains succès localement

Le comportement exact dépend de l'émulateur pris en charge.

## 9. Émulateurs validés et testés

Les émulateurs suivants sont actuellement validés et testés avec `Compagnon` :

- `RetroArch`
- `RALibretro`
- `Flycast`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `BizHawk`
- `Dolphin`
- `RANes`
- `RAVBA`
- `RASnes9x`
- `RAP64`

Remarques :

- `RetroArch` est pris en charge en version portable et en version installable
- le niveau exact de détection peut varier selon l'émulateur, sa configuration locale et les sources disponibles
- certains émulateurs permettent aussi la relance via `Rejouer`
- `LunaProject64` n'est plus pris en charge

## 10. Données locales

Les données utilisateur sont stockées dans :

`%AppData%\RA-Compagnon`

On y trouve notamment :

- `user.json`
- `configuration.json`
- `game.json`
- `achievement.json`
- `achievements_list.json`

## 11. Conseils d'usage

- utilise `Recharger` si l'affichage ne semble pas à jour
- utilise `Passer` si tu veux avancer dans les succès non débloqués
- utilise `Détails` si tu veux une vue plus complète du jeu
- laisse `Compagnon` terminer une synchronisation avant de conclure qu'une information manque

## 12. Si quelque chose semble ne pas fonctionner

En cas de doute :

1. clique sur `Recharger`
2. vérifie que ton compte est bien connecté
3. vérifie que l'émulateur lancé est bien un émulateur valide
4. ouvre l'aide de l'application si tu dois consulter les journaux ou les emplacements détectés
