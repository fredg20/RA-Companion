# Version

## Version courante

- Nom : `RA-Compagnon`
- Version : `1.0.8`
- Statut : base stable en cours d'évolution
- Cible : `Windows x64`
- Livrable principal : `dist/RA.Compagnon-win-x64`
- Archive de release : `dist/RA.Compagnon-win-x64-1.0.8.zip`

## Contenu de cette version

- interface principale stabilisée
- détection locale consolidée pour les émulateurs validés
- gestion plus robuste des états de chargement du jeu courant
- affichage des rétrosuccès rendu plus stable visuellement
- aide utilisateur enrichie et plus lisible
- affichage des sources locales, des emplacements et des journaux par émulateur
- mise à jour intégrée de l'application
- téléchargement de mise à jour corrigé
- ajout d'un mapping manuel pour corriger l'émulateur détecté ou son emplacement
- ajout du bouton `Rejouer` pour relancer un jeu local détecté
- ajout du bouton `Recharger` pour forcer un rechargement complet du jeu courant
- ajout du bouton `Passer` pour repousser localement un rétrosuccès non débloqué
- faisabilité des rétrosuccès basée sur le ratio `déblocages / joueurs distincts`
- interface WPF réorganisée avec boutons d'action déplacés à l'intérieur des cartes
- restauration locale au démarrage avec indicateur discret de synchronisation
- libellé `Dernier jeu` utilisé à la place de `Actif récemment`
- en-tête réorganisé avec `Recharger` près de `Profil`
- bouton `Rejouer` ancré en bas à gauche de la section `Jeu en cours`
- bouton `Détails` ancré en bas à droite de la section `Jeu en cours`
- suppression des boutons manuels du carrousel d'image tout en conservant sa rotation
- mode d'affichage des rétrosuccès mémorisé d'une session à l'autre
- désélection automatique d'un rétrosuccès lors d'un changement de mode d'affichage
- au démarrage, retour automatique sur le premier rétrosuccès non débloqué de la liste
- sections plein écran corrigées pour éviter les coupures sous la barre d'état
- alignement visuel renforcé des zones de progression et des actions de carte
- distinction visuelle `Softcore` / `Hardcore` ajoutée pour les rétrosuccès débloqués
- infobulles de la grille enrichies avec le mode `Softcore` ou `Hardcore`
- badges `Hardcore` mis en valeur par un contour doré et un halo léger dans la grille
- même mise en valeur dorée appliquée au badge de la section `Rétrosuccès en cours`
- ajout du manuel utilisateur `INSTRUCTION.md`

## Émulateurs validés dans cette version

- `RetroArch`
- `RALibretro`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `Flycast`
- `BizHawk`
- `Dolphin`
- `RANes`
- `RAVBA`
- `RASnes9x`
- `RAP64`

## Notes de release

- le dossier publié `RA.Compagnon-win-x64` ne doit pas être restructuré manuellement
- le build `dist` est généré via `build.ps1`
- la préparation de release peut être figée via `Prepare-Release.ps1`
- une archive versionnée de release est générée dans `dist`
- `update.json` doit pointer vers une archive versionnée exacte, et non vers `releases/latest`
- `publishedAt` est synchronisé avec la date réelle de publication GitHub lors d'une release

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
- aide utilisateur enrichie avec une section dédiée aux journaux
- ajout d'une vue détaillée du jeu courant en modale

### Version 1.0.3

- fonction de mise à jour intégrée jusqu'à l'installation et au redémarrage
- nettoyage du dossier `updates` après installation
- ajout du support `RACache` pour `RANes`, `RAVBA` et `RASnes9x`
- suppression des détections transitoires parasites via `explorer`

### Version 1.0.4

- détection de l'emplacement des émulateurs
- indication du niveau de confiance de détection : excellente, bonne ou fragile
- renforcement de la reconnaissance par métadonnées d'exécutable et chemin réel
- ajout d'un mapping manuel utilisateur pour corriger l'émulateur détecté ou son emplacement
- vrai support local ajouté pour `Flycast` via `flycast.log`, avec le chemin du jeu lancé en secours
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
- en mode `Dernier jeu`, affichage forcé sur le dernier jeu mémorisé par `GameID`
- bouton `Details` déplacé sur la ligne d'en-tête de la carte principale
- ajustements visuels de la carte `Rétrosuccès en cours` et de ses boutons de navigation

### Version 1.0.6

- préparation de la release versionnée `1.0.6`
- manifeste de mise à jour aligné sur `v1.0.6`
- archive de release attendue : `RA.Compagnon-win-x64-1.0.6.zip`
- modale Aide rendue plus réactive à l'ouverture
- mémorisation automatique de l'emplacement des émulateurs validés ouverts
- journalisation de la sonde locale et de l'affichage des informations du jeu
- lecture des journaux locaux fiabilisée pour les changements de jeu, le `GameID` et les succès
- ajout du bouton `Rejouer` pour relancer un jeu local détecté
- extension de `Rejouer` à l'ensemble des émulateurs validés à partir de leurs sources locales
- prise en charge spécifique de `RetroArch`, `RALibretro` et `Dolphin` pour la relance adaptée
- masquage du bouton `Rejouer` pendant l'état `En jeu`
- conservation plus stable des informations affichées sur le jeu courant
- ajout et validation des supports `BizHawk`, `Dolphin` et `RAP64`
- retrait du support `LunaProject64`

### Version 1.0.7

- préparation de la release versionnée `1.0.7`
- manifeste de mise à jour aligné sur `v1.0.7`
- archive de release attendue : `RA.Compagnon-win-x64-1.0.7.zip`
- amélioration du français dans la documentation visible
- `RALibretro` entièrement validé pour `Rejouer`
- `Dolphin` ajouté aux émulateurs validés et testés
- retrait du support `LunaProject64`
- poursuite du nettoyage de `MainWindow` et de la couche `ViewModel`
- ajout de `Recharger`, `Passer` et de l'indicateur `Synchronisation...`
- estimation simplifiée de faisabilité des rétrosuccès
- ajustements de layout sur les cartes `Jeu en cours`, `Rétrosuccès en cours` et `Rétrosuccès du jeu`
- remplacement du libellé `Actif récemment` par `Dernier jeu`
- réorganisation de l'en-tête supérieur et des actions `Recharger`, `Rejouer` et `Détails`
- mémorisation du mode d'affichage `Normal`, `Aléatoire`, `Facile` ou `Difficile`
- désélection d'un rétrosuccès épinglé lors d'un changement de mode
- au démarrage, retour sur le premier rétrosuccès encore non débloqué
- progression textuelle harmonisée avec le libellé `succès`
- alignement corrigé des informations sous la barre de progression
- sections principales ajustées pour ne plus être coupées en plein écran
- actions de la carte `Jeu en cours` repositionnées avec `Rejouer` en bas à gauche et `Détails` en bas à droite
- suppression des boutons manuels du carrousel d'image tout en conservant la rotation automatique
- recentrage du carrousel d'image et suppression d'espaces superflus dans la carte `Jeu en cours`

### Version 1.0.8

- préparation de la release versionnée `1.0.8`
- manifeste de mise à jour aligné sur `v1.0.8`
- archive de release attendue : `RA.Compagnon-win-x64-1.0.8.zip`
- synchronisation ciblée ajoutée à chaque changement d'état du jeu
- distinction `Softcore` / `Hardcore` ajoutée dans l'affichage du rétrosuccès courant
- infobulles de la grille mises à jour pour afficher le mode d'obtention d'un succès débloqué
- style visuel `Hardcore` ajouté avec contour doré et halo léger dans la grille des badges
- style visuel `Hardcore` également appliqué au badge de la section `Rétrosuccès en cours`
- ajout d'un manuel d'utilisation dans `INSTRUCTION.md`
