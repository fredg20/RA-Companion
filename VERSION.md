# Version

## Version courante

- Nom : `RA-Compagnon`
- Version : `1.0.6`
- Statut : version de travail stable
- Cible de livraison : `Windows x64`
- Livrable principal : `dist/RA.Compagnon-win-x64`
- Archive de release : `dist/RA.Compagnon-win-x64-1.0.6.zip`

## Contenu de cette version

- interface principale stabilisée
- détection locale consolidée pour les émulateurs validés
- gestion plus robuste des états de chargement du jeu courant
- grille des rétrosuccès plus stable visuellement
- aide utilisateur enrichie
- affichage des sources locales, des emplacements et des logs par émulateur
- mise à jour intégrée de l'application
- téléchargement de mise à jour corrigé
- ajout d'un mapping manuel pour corriger l'émulateur détecté ou son emplacement
- ajout du bouton `Rejouer` pour relancer un jeu local détecté

## Émulateurs validés dans cette version

- `RetroArch`
- `RALibretro`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `Flycast`
- `RANes`
- `RAVBA`
- `RASnes9x`

## Remarques

- le dossier publié `RA.Compagnon-win-x64` ne doit pas être restructuré manuellement
- le build `dist` est généré via `build.ps1`
- la préparation de release peut être figée via `Prepare-Release.ps1`
- une seule archive est conservée : l'archive versionnée de release
- `update.json` doit pointer vers une archive versionnée exacte, pas vers `releases/latest`
- `publishedAt` est synchronisé automatiquement avec la vraie date de publication GitHub lors d'une release

## Historique local

### Version 1.0

- première base publiable de `Compagnon`
- affichage du dernier jeu joué et de la progression principale
- connexion du compte RetroAchievements
- persistance locale et restauration rapide au démarrage
- premières détections locales d'émulateur

### Version 1.0.2

- orchestration d'état renforcée
- couche émulateurs unifiée
- pipeline de chargement du jeu consolidé
- stabilisation des animations et de la liste des succès
- aide utilisateur enrichie avec la section des logs
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
- correction du téléchargement de mise à jour pour finaliser correctement le package `.zip`
- sécurisation du processus de release avec archive versionnée et manifeste figé

### Version 1.0.5

- préparation de la release versionnée `1.0.5`
- manifeste de mise à jour aligné sur `v1.0.5`
- archive de release attendue : `RA.Compagnon-win-x64-1.0.5.zip`
- modale Aide réorganisée avec des sections rabattables
- section `Logs des émulateurs` rendue plus lisible avec un panneau rabattable par émulateur
- instructions des émulateurs réécrites en français dans la modale Aide
- barre de défilement de la modale Aide visible uniquement au survol
- détection plus stricte des titres locaux pour éviter l'affichage d'un titre provenant d'une fenêtre non liée à un émulateur validé
- détection locale `RetroArch` rendue plus réactive au démarrage de la surveillance
- en mode `Actif récemment`, affichage forcé sur le dernier jeu mémorisé par `GameID`
- bouton `Détails` déplacé sur la ligne d'en-tête de la carte principale
- ajustements visuels de la carte `Rétrosuccès en cours` et de ses boutons de navigation

### Version 1.0.6

- préparation de la release versionnée `1.0.6`
- manifeste de mise à jour aligné sur `v1.0.6`
- archive de release attendue : `RA.Compagnon-win-x64-1.0.6.zip`
- modale Aide rendue plus réactive à l'ouverture
- mémorisation automatique de l'emplacement des émulateurs validés ouverts
- journalisation de la sonde locale et de l'affichage des informations du jeu
- lecture de `retroarch.log` fiabilisée pour les changements de jeu et le `GameID`
- ajout du bouton `Rejouer` pour relancer un jeu local détecté
- extension de `Rejouer` à l'ensemble des émulateurs validés via leurs sources locales
- prise en charge spécifique de `RetroArch` avec relance via le bon core
- masquage du bouton `Rejouer` pendant l'état `En jeu`
- conservation plus stable des informations affichées sur le jeu courant
