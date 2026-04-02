# Version

## Version courante

- Nom : `RA-Compagnon`
- Version : `1.0.4`
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
- indicateur des sources locales, des emplacements et des logs par émulateur
- mise à jour de l'application intégrée
- ajout d'un mapping manuel utilisateur pour corriger un émulateur détecté ou son emplacement

## Émulateurs validés dans cette version

- `RetroArch`
- `RALibretro`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `Luna's Project64`
- `Flycast`
- `RANes`
- `RAVBA`
- `RASnes9x`

## Remarques

- le dossier publié `RA.Compagnon-win-x64` ne doit pas être restructuré manuellement
- le build `dist` est généré via `build.ps1`
- la version pourra être affinée plus tard si on veut passer à un cycle plus formel : `0.x`, `1.x`, etc.

## Historique local

### Version 1.0

- première base publiable de `Compagnon`
- affichage du dernier jeu joué et de la progression principale
- connexion utilisateur RetroAchievements
- persistance locale et restauration rapide au démarrage
- premières détections locales d'émulateur

### Version 1.0.2

- orchestration d'état renforcée
- couche émulateurs unifiée
- pipeline de chargement du jeu consolidé
- stabilisation des animations et de la liste des succès
- aide utilisateur enrichie avec la section logs
- ajout d'une vue détaillée du jeu courant en modale

### Version 1.0.3

- fonction de mise à jour intégrée jusqu'à l'installation et au redémarrage
- nettoyage du dossier `updates` après installation
- ajout du support `RACache` pour `RANes`, `RAVBA` et `RASnes9x`
- suppression des détections transitoires parasites via `explorer`

### Version 1.0.4

- détection de l'emplacement des émulateurs
- indication de confiance de détection : excellente, bonne ou fragile
- renforcement de la reconnaissance par métadonnées d'exécutable et chemin réel
- ajout d'un mapping manuel utilisateur pour corriger l'émulateur détecté ou son emplacement
- vrai support local ajouté pour `Flycast` via `flycast.log` et le chemin du jeu lancé en secours
