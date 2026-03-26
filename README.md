# RA-Compagnon

Compagnon Windows pour suivre un compte RetroAchievements et afficher rapidement :

- le dernier jeu joue
- sa progression
- les retrosucces du jeu
- les succes recents du compte

## Etat actuel

`RA-Compagnon` est maintenant centre sur :

- les appels a l'API RetroAchievements
- la persistance locale
- la restauration rapide au demarrage
- l'affichage du jeu et des retrosucces

Le projet ne depend plus d'un emulateur ni d'une sonde locale.

## Fonctionnalites

- affichage du dernier jeu joue
- affichage du titre, de la console, de la date, du genre et du developpeur
- affichage des visuels du jeu
- affichage d'un retrosucces mis en avant
- grille complete des retrosucces du jeu
- succes recents du compte
- ordre de grille `Normal`, `Aleatoire`, `Facile`, `Difficile`
- restauration locale au demarrage

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

```powershell
dotnet build RA.Compagnon.sln -m:1
```

Build copie dans `dist` :

```powershell
./build.ps1
```
