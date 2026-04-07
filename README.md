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
- succès récents du compte
- détection locale du jeu en cours via certains émulateurs pris en charge
- ordre de grille `Normal`, `Aléatoire`, `Facile`, `Difficile`
- restauration locale au démarrage

## Validation émulateurs

Validation confirmée :

- `Flycast`
  - détection locale du processus
  - extraction du titre du jeu depuis la fenêtre
  - résolution rapide vers le `Game ID` RetroAchievements
  - priorité locale correcte tant que l'émulateur est ouvert
- `DuckStation`
  - détection locale du processus
  - extraction du titre du jeu via fenêtre, automatisation ou fallback local
  - résolution vers le `Game ID` RetroAchievements
  - maintien de l'état local même si le titre disparaît temporairement
- `PCSX2`
  - détection locale du processus
  - filtrage des fenêtres de dialogue et outils internes
  - extraction du titre du vrai jeu depuis la fenêtre
  - résolution vers le `Game ID` RetroAchievements, y compris via le catalogue local
- `PPSSPP`
  - détection locale du processus
  - extraction du titre du jeu depuis la fenêtre avec nettoyage du serial PSP
  - résolution vers le `Game ID` RetroAchievements
  - appui sur le catalogue local quand il est déjà disponible
- `RetroArch`
  - détection locale du processus `retroarch`
  - lecture prioritaire du `Game ID` RetroAchievements depuis le dernier log horodaté
  - mise à jour correcte du `Game ID` lors des changements de jeu
  - application locale du bon jeu même quand la fenêtre n'expose pas le titre
- `RALibretro`
  - détection locale du processus `RALibretro`
  - lecture prioritaire du `Game ID` RetroAchievements depuis le dernier fichier `Data/<GameID>.json` modifié dans le `RACache`
  - suivi correct des changements de jeu successifs
  - application locale du bon jeu sans dépendre du titre de fenêtre

Exclusion confirmée :

- `BizHawk`
  - détection de jeu possible par titre ou ROM récente, mais pas de source locale fiable pour le `Game ID`
  - pas de `RACache`, pas de log RA exploitable, pas de `Game ID` lisible dans les fichiers `.State.rap` ou `SaveRAM`
  - support volontairement exclu pour éviter des détections fragiles

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

## Structure

- `RA.Compagnon/MainWindow.xaml` : interface principale
- `RA.Compagnon/MainWindow.xaml.cs` : point d'entrée de la fenêtre
- `RA.Compagnon/Services/ClientRetroAchievements.cs` : appels API
- `RA.Compagnon/Services/ServiceConfigurationLocale.cs` : persistance locale
- `RA.Compagnon/Services/ServiceTraductionTexte.cs` : traduction de certains textes
- `RA.Compagnon/Modeles/Api` : modèles API
- `RA.Compagnon/Modeles/Local` : modèles locaux

## Build

Build de la solution :

```powershell
dotnet build RA.Compagnon.sln -m:1
```

Generation du livrable autonome `RA.Compagnon-win-x64` dans `dist` :

```powershell
./build.ps1
```

Tests ciblés :

```powershell
powershell -ExecutionPolicy Bypass -File .\run-tests.ps1
```

Checklist de validation :

- [CHECKLIST.md](CHECKLIST.md)
