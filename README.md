# RA-Compagnon

Compagnon Windows pour suivre un compte RetroAchievements avec une interface simple, rapide et agréable.

## Vision

`Compagnon` n'essaie pas d'être un clone de `RetroAchievements Layout Manager`.

Son positionnement est différent :

- offrir une expérience plus claire et plus moderne
- rendre la progression plus agréable à consulter
- rester stable et rapide au quotidien
- être utile sans dépendre d'un émulateur

En une phrase :

`Compagnon` est une application de bureau pensée pour suivre ses jeux et ses rétrosuccès avec confort.

## Public visé

- joueurs RetroAchievements sur PC
- utilisateurs qui veulent retrouver rapidement leur progression
- personnes qui préfèrent une application lisible, stable et simple à utiliser

## État actuel

`RA-Compagnon` est maintenant centré sur :

- les appels à l'API RetroAchievements
- la persistance locale
- la restauration rapide au démarrage
- l'affichage du jeu et des rétrosuccès

Le projet ne dépend plus d'un émulateur ni d'une sonde locale.

## Fonctionnalités actuelles

- affichage du dernier jeu joué
- affichage du titre, de la console, de la date, du genre et du développeur
- affichage des visuels du jeu
- affichage d'un rétrosuccès mis en avant
- grille complète des rétrosuccès du jeu
- succès récents du compte
- ordre de grille `Normal`, `Aléatoire`, `Facile`, `Difficile`
- restauration locale au démarrage

## Direction produit

Les prochaines évolutions les plus naturelles sont :

- améliorer encore la lisibilité de l'interface
- enrichir la navigation entre jeux et succès
- ajouter des fonctions utiles sans complexifier l'application

Ce que le projet ne cherche pas à faire pour l'instant :

- détection en direct d'émulateur
- dépendance à `RetroArch` ou à un autre émulateur
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

Génération du livrable dans `dist` :

```powershell
./build.ps1
```
