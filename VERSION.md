# Version

## Version courante

- Nom : `RA-Compagnon`
- Version : `1.0.1`
- Statut : version de travail stable
- Cible de livraison : `Windows x64`
- Livrable principal : `dist/RA.Compagnon-win-x64`
- Archive principale : `dist/RA.Compagnon-win-x64.zip`

## Contenu de cette version

- interface principale stabilisée
- détection locale consolidée pour les émulateurs validés
- gestion plus robuste des états de chargement du jeu courant
- grille des rétrosuccès plus stable visuellement
- aide utilisateur enrichie
- indicateur des sources locales et des logs par émulateur

## Émulateurs validés dans cette version

- `RetroArch`
- `RALibretro`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `Luna's Project64`
- `Flycast`

## Remarques

- le dossier publié `RA.Compagnon-win-x64` ne doit pas être restructuré manuellement
- le build `dist` est généré via `build.ps1`
- la version peut être affinée plus tard avec une numérotation explicite si on veut passer à un vrai cycle `0.x`, `1.0`, etc.

## Historique local

### Version 1.0

- première base publiable de `Compagnon`
- affichage du dernier jeu joué et de la progression principale
- connexion utilisateur RetroAchievements
- persistance locale et restauration rapide au démarrage
- premières détections locales d'émulateur

### Version 1.0.1

- orchestration d'état renforcée
- couche émulateurs unifiée
- pipeline de chargement du jeu consolidé
- stabilisation des animations et de la liste des succès
- aide utilisateur enrichie avec la section logs
- ajout d'une vue détaillée du jeu courant en modale
