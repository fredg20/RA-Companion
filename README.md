# RA-Compagnon

Compagnon Windows pour suivre un compte RetroAchievements avec une interface simple, rapide et agreable.

## Vision

`Compagnon` n'essaie pas d'etre un clone de `RetroAchievements Layout Manager`.

Son positionnement est different :

- offrir une experience plus claire et plus moderne
- rendre la progression plus agreable a consulter
- rester stable et rapide au quotidien
- etre utile sans dependre d'un emulateur

En une phrase :

`Compagnon` est une application de bureau pensée pour suivre ses jeux et ses retrosucces avec confort.

## Public vise

- joueurs RetroAchievements sur PC
- utilisateurs qui veulent retrouver rapidement leur progression
- personnes qui preferent une application lisible, stable et simple a utiliser

## Etat actuel

`RA-Compagnon` est maintenant centre sur :

- les appels a l'API RetroAchievements
- la persistance locale
- la restauration rapide au demarrage
- l'affichage du jeu et des retrosucces

Le projet ne depend plus d'un emulateur ni d'une sonde locale.

## Fonctionnalites actuelles

- affichage du dernier jeu joue
- affichage du titre, de la console, de la date, du genre et du developpeur
- affichage des visuels du jeu
- affichage d'un retrosucces mis en avant
- grille complete des retrosucces du jeu
- succes recents du compte
- ordre de grille `Normal`, `Aleatoire`, `Facile`, `Difficile`
- restauration locale au demarrage

## Direction produit

Les prochaines evolutions les plus naturelles sont :

- ameliorer encore la lisibilite de l'interface
- enrichir la navigation entre jeux et succes
- ajouter des fonctions utiles sans complexifier l'application

Ce que le projet ne cherche pas a faire pour l'instant :

- detection live d'emulateur
- dependance a `RetroArch` ou a un autre emulateur
- logique memoire live
- fonctions fragiles qui degradent la stabilite

## Stockage local

Les donnees utilisateur sont stockees dans :

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
- `RA.Compagnon/MainWindow.xaml.cs` : point d'entree de la fenetre
- `RA.Compagnon/Services/ClientRetroAchievements.cs` : appels API
- `RA.Compagnon/Services/ServiceConfigurationLocale.cs` : persistance locale
- `RA.Compagnon/Services/ServiceTraductionTexte.cs` : traduction de certains textes
- `RA.Compagnon/Modeles/Api` : modeles API
- `RA.Compagnon/Modeles/Local` : modeles locaux

## Build

Build de la solution :

```powershell
dotnet build RA.Compagnon.sln -m:1
```

Generation du livrable dans `dist` :

```powershell
./build.ps1
```
