# RA-Compagnon

Compagnon Windows pour suivre un compte RetroAchievements avec une interface simple, rapide et agréable.

## Vision

`Compagnon` n'essaie pas d'être un clone de `RetroAchievements Layout Manager`.

Son positionnement est différent :

- offrir une expérience plus claire et plus moderne
- rendre la progression plus agréable à consulter
- rester stable et rapide au quotidien
- être utile même sans dépendre uniquement d'un émulateur

En une phrase :

`Compagnon` est une application de bureau pensée pour suivre ses jeux et ses rétrosuccès avec confort.

## Public visé

- joueurs RetroAchievements sur PC
- utilisateurs qui veulent retrouver rapidement leur progression
- personnes qui préfèrent une application lisible, stable et simple à utiliser

## État actuel

`RA-Compagnon` est maintenant centré sur :

- les appels à l'API RetroAchievements
- la détection locale légère d'émulateur pour accélérer l'affichage du jeu courant
- la persistance locale
- la restauration rapide au démarrage
- l'affichage du jeu et des rétrosuccès

L'application reste utilisable sans émulateur, mais sait désormais exploiter une sonde locale quand elle est disponible.

## Fonctionnalités actuelles

- affichage du dernier jeu joué
- affichage du titre, de la console, de la date, du genre et du développeur
- affichage des visuels du jeu
- affichage d'un rétrosuccès mis en avant
- grille complète des rétrosuccès du jeu
- bouton `Recharger` pour forcer un rechargement du jeu courant
- bouton `Rejouer` pour relancer le jeu courant quand le contexte local le permet
- bouton `Passer` pour repousser localement un rétrosuccès non débloqué et afficher le suivant
- estimation simple de faisabilité d'un rétrosuccès à partir du ratio `déblocages / joueurs distincts`
- succès récents du compte
- détection locale du jeu en cours via certains émulateurs pris en charge
- ordre de grille `Normal`, `Aléatoire`, `Facile`, `Difficile`, avec mémorisation du mode choisi
- restauration locale au démarrage
- retour automatique sur le premier rétrosuccès non débloqué au redémarrage
- indicateur discret de synchronisation quand l'état local restauré est en cours de rafraîchissement

## Validation émulateurs

Validation confirmée :

- `RetroArch`
  - détection locale du processus `retroarch`
  - lecture prioritaire du `Game ID` RetroAchievements depuis le dernier log horodaté
  - mise à jour correcte du `Game ID` lors des changements de jeu
  - succès et `Rejouer` validés
- `RALibretro`
  - détection locale du processus `RALibretro`
  - lecture prioritaire du `Game ID` RetroAchievements depuis le dernier fichier `Data/<GameID>.json` modifié dans le `RACache`
  - succès validés
  - `Rejouer` validé avec la vraie ligne de commande `--core --system --game`
- `Flycast`
  - détection locale du processus
  - lecture locale du jeu et des succès depuis `flycast.log`
  - résolution fiable du `Game ID`
  - `Rejouer` validé
- `DuckStation`
  - détection locale du processus
  - extraction du titre du jeu via fenêtre, automatisation ou fallback local
  - résolution vers le `Game ID` RetroAchievements
  - succès et `Rejouer` validés
- `PCSX2`
  - détection locale du processus
  - filtrage des fenêtres de dialogue et outils internes
  - extraction du titre du vrai jeu depuis la fenêtre
  - résolution vers le `Game ID` RetroAchievements, y compris via le catalogue local
  - succès et `Rejouer` validés
- `PPSSPP`
  - détection locale du processus
  - extraction du titre du jeu depuis la fenêtre avec nettoyage du serial PSP
  - résolution vers le `Game ID` RetroAchievements
  - appui sur le catalogue local quand il est déjà disponible
  - succès et `Rejouer` validés
- `BizHawk`
  - lecture du jeu et des succès depuis `retroachievements-game-log.json`
  - suivi des déblocages par changement de `unlocked_softcore` ou `unlocked_hardcore`
  - `Rejouer` et aide validés
- `Dolphin`
  - lecture du jeu et des succès depuis `dolphin.log`
  - résolution du chemin du jeu pour `Rejouer` via les fichiers de configuration locaux
  - `Rejouer` et aide validés
- `RANes`
  - détection via `RACache` et configuration locale
  - succès et `Rejouer` validés
- `RAVBA`
  - détection via `RACache` et configuration locale
  - succès et `Rejouer` validés
- `RASnes9x`
  - détection via `RACache` et configuration locale
  - succès et `Rejouer` validés
- `RAP64`
  - détection via `RACache` et configuration `Project64`
  - succès et `Rejouer` validés

Exclusion confirmée :

- `LunaProject64`
  - support retiré du projet pour éviter les conflits avec `RAP64`
  - maintenance et détection simplifiées autour de la seule variante `Project64` réellement validée

## Direction produit

Les prochaines évolutions les plus naturelles sont :

- améliorer encore la lisibilité de l'interface
- enrichir la navigation entre jeux et succès
- ajouter des fonctions utiles sans complexifier l'application

Ce que le projet ne cherche pas à faire pour l'instant :

- logique de mémoire en direct
- fonctions fragiles qui dégradent la stabilité

## Stockage local

Les données utilisateur sont stockées dans :

`%AppData%\RA-Compagnon`

Fichiers principaux :

- `user.json`
- `configuration.json`
- `game.json`
- `achievement.json`
- `achievements_list.json`
- `image_cache`

## Prérequis

- Windows
- SDK `.NET 9`

## Structure

- `RA.Compagnon/MainWindow.xaml` : interface principale
- `RA.Compagnon/MainWindow.xaml.cs` : point d'entrée de la fenêtre
- `RA.Compagnon/Services/ClientRetroAchievements.cs` : appels API
- `RA.Compagnon/Services/ServiceConfigurationLocale.cs` : persistance locale
- `RA.Compagnon/Services/ServiceEvaluationFaisabiliteSucces.cs` : estimation de faisabilité des rétrosuccès
- `RA.Compagnon/Services/ServiceTraductionTexte.cs` : traduction de certains textes
- `RA.Compagnon/Modeles/Api` : modèles API
- `RA.Compagnon/Modeles/Local` : modèles locaux
- `RA.Compagnon/ViewModels` : couche MVVM utilisée par l'interface
