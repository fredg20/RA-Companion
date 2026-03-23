# RA-Compagnon

Compagnon Windows pour suivre un compte RetroAchievements et afficher, en quasi temps réel, le jeu détecté, son état et ses rétrosuccès.

## Objectif

`RA-Compagnon` est une application WPF qui combine :

- une détection locale d'émulateur et de jeu
- des appels à l'API RetroAchievements
- une interface compacte orientée affichage en direct
- une restauration locale rapide au démarrage

L'application essaie d'afficher au plus vite un jeu plausible, puis d'enrichir cet affichage avec les données officielles RetroAchievements.

## Fonctionnalités actuelles

### Jeu en cours / Dernier jeu joué

- affiche le jeu courant si un émulateur local est détecté
- bascule automatiquement sur `Dernier jeu joué` si aucun émulateur n'est actif
- affiche le titre du jeu, la console, l'année, le genre, le développeur et les visuels du jeu
- peut afficher plusieurs visuels via un petit carrousel horizontal avec flèches
- conserve les informations déjà valides pendant les rafraîchissements pour éviter les clignotements
- recharge le dernier jeu sauvegardé au démarrage avant le premier rafraîchissement API

### Détection locale

- sonde locale rapide toutes les `500 ms`
- détection d'émulateurs connus comme `RetroArch`, `RALibRetro`, `BizHawk`, `Flycast`, `Dolphin`, `PPSSPP`, `DuckStation`, `PCSX2`, `Project64`
- estimation du jeu à partir :
  - du processus
  - du titre de fenêtre
  - de la ligne de commande
  - du chemin de ROM / image disque
- résolution par hash de fichier quand possible pour retrouver le bon `gameId` RetroAchievements
- meilleure prise en charge des émulateurs multi-systèmes grâce aux extensions et indices de core

### API RetroAchievements

Le client actuel consomme notamment :

- le profil utilisateur
- le résumé utilisateur
- le jeu et la progression utilisateur
- le dernier jeu joué
- les consoles
- les jeux système et leurs hashes

Le projet met aussi en cache certaines données lourdes :

- les consoles
- les jeux système et leurs hashes
- les images distantes

### Rétrosuccès

- une zone `Rétrosuccès en cours` affiche le succès actuellement mis en avant
- le badge peut être affiché en niveaux de gris avec une opacité réduite
- le titre, la description et les points / rétropoints sont affichés
- une zone `Rétrosuccès du jeu` affiche la grille complète des badges du jeu
- les badges ont des coins arrondis
- l'espacement horizontal de la grille s'ajuste à la largeur disponible
- la grille peut rebondir verticalement quand son contenu dépasse la zone visible
- le survol d'un badge met cette animation en pause
- la molette permet de déplacer la grille, puis l'animation reprend ensuite

### rcheevos

- un bridge natif `rcheevos` est intégré à l'application
- `Compagnon` peut enregistrer des définitions de succès et interroger un `Progress Indicator`
- une source mémoire `RetroArch` existe via l'interface réseau officielle
- une source passive `RALibRetro` existe via le bloc `ACHV` embarqué dans les fichiers `.state`

Limite actuelle :

- l'indicateur `Measured` ne peut être affiché que si les données source sont réellement exploitables pour le succès courant
- selon les jeux et les données renvoyées par l'API, cet indicateur peut donc rester vide

### Interface

- application WPF sur `net9.0-windows`
- base de fenêtre `WPF-UI` (`FluentWindow`)
- barre de titre personnalisée
- textes informatifs sélectionnables pour pouvoir être copiés
- zone `Démarrage` actuellement masquée provisoirement

## Restauration au démarrage

Au lancement, `RA-Compagnon` recharge l'état local sauvegardé avant le premier rafraîchissement réseau.

Sont restaurés si disponibles :

- les informations utilisateur
- la géométrie de la fenêtre
- le dernier jeu affiché
- le rétrosuccès en cours
- la grille des rétrosuccès du jeu

Pour accélérer la restauration visuelle, les images distantes utilisées récemment sont aussi conservées dans un cache disque local.

## Stockage local

Les fichiers utilisateur sont stockés dans :

`%AppData%\RA-Compagnon`

### `user.json`

Contient les informations utilisateur :

- pseudo RetroAchievements
- clé Web API

Ce fichier n'est réécrit que lorsque de nouvelles données sont validées dans la modale de connexion.

### `configuration.json`

Contient la configuration générale :

- position de fenêtre
- taille de fenêtre

### `game.json`

Contient le dernier jeu affiché et son état d'affichage :

- identifiant du jeu
- statut `Jeu en cours` / `Dernier jeu joué`
- titre
- détails affichés
- progression affichée
- état du jeu
- image / console / date / genre / développeur

### `achievement.json`

Contient l'état du rétrosuccès en cours :

- identifiant du jeu
- identifiant du succès
- titre
- description
- détails de points / rétropoints
- indicateur `Measured` si disponible
- chemin de l'image du badge
- texte visuel de secours

### `achievements_list.json`

Contient l'état de la grille des rétrosuccès affichés :

- identifiant du jeu
- liste des badges affichés
- titre de chaque succès
- chemin de l'image utilisée pour chaque badge

### `image_cache`

Contient le cache disque des images distantes déjà chargées, pour accélérer la restauration au démarrage :

- jaquettes
- badges
- icônes de console
- autres visuels réseau déjà utilisés

## Structure du projet

- `RA.Compagnon/MainWindow.xaml` : interface principale
- `RA.Compagnon/MainWindow.xaml.cs` : logique d'affichage, orchestration UI, animation et persistance d'état
- `RA.Compagnon/Services/ClientRetroAchievements.cs` : appels API RetroAchievements
- `RA.Compagnon/Services/SondeJeuLocal.cs` : détection locale des émulateurs et jeux
- `RA.Compagnon/Services/ServiceHachageJeuLocal.cs` : calcul des empreintes locales
- `RA.Compagnon/Services/ServiceConfigurationLocale.cs` : persistance locale
- `RA.Compagnon/Services/ServiceMemoireRetroArch.cs` : lecture mémoire RetroArch via l'interface réseau officielle
- `RA.Compagnon/Services/ServiceRcheevos.cs` : orchestration des sources `rcheevos`
- `RA.Compagnon/Services/ServiceRcheevosRalibretro.cs` : source passive `RALibRetro` via le bloc `ACHV` embarqué dans les `.state`
- `RA.Compagnon/Services/ServiceTraductionTexte.cs` : traduction de certains textes vers le français
- `RA.Compagnon/Modeles/Api` : DTO liés à l'API RetroAchievements
- `RA.Compagnon/Modeles/Local` : modèles locaux de configuration et d'état
- `native/ra_compagnon_rcheevos_bridge` : bridge natif `rcheevos`

## Build

### Build standard

```powershell
dotnet build RA.Compagnon.sln -m:1
```

### Build complet copié dans `dist`

```powershell
./build.ps1
```

Le script :

- tente un build `Release`
- copie la build exploitable dans `dist/RA.Compagnon`
- arrête l'application si elle est encore ouverte pour éviter les erreurs de copie

## Instruction

### Premier lancement

1. Lance `RA.Compagnon`.
2. Entre ton pseudo RetroAchievements et ta clé Web API dans la modale `Connexion`.
3. Valide pour enregistrer `user.json` et initialiser le reste de l'état local.

### Utilisation courante

1. Lance `RA.Compagnon`.
2. Ouvre un émulateur compatible avec un jeu RetroAchievements.
3. Laisse l'application détecter le jeu localement.
4. Attends l'enrichissement par l'API pour voir :
   - le jeu courant ou le dernier jeu joué
   - le rétrosuccès en cours
   - la progression
   - la grille des rétrosuccès du jeu

### Redémarrage

- Au démarrage suivant, l'application recharge d'abord l'état local sauvegardé.
- Les données fraîches de l'API et de la détection locale remplacent ensuite cet état si nécessaire.

## État actuel

Le projet est aujourd'hui centré sur :

- la rapidité de détection
- la continuité visuelle pendant les rafraîchissements
- la restauration locale au démarrage
- l'affichage des rétrosuccès du jeu courant
- une persistance locale découpée par type de données
- un bridge natif `rcheevos` déjà intégré au build
- une lecture mémoire `RetroArch` et une voie passive `RALibRetro` via les savestates

Le code compile proprement et le dossier `dist/RA.Compagnon` peut être régénéré via `build.ps1`.
