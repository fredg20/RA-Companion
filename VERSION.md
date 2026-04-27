# Version

## État courant

- Application : `RA-Compagnon`
- Version applicative : `1.1`
- Plateforme cible : `Windows x64`
- Interface active : `WPF`
- Livrable local : `dist/RA.Compagnon-win-x64`
- Dernière archive de release prête : `dist/RA.Compagnon-win-x64-1.1.zip`
- Manifeste de mise à jour public : `update.json` préparé pour `1.1`

## Contenu fonctionnel livré

- affichage du dernier jeu et du jeu en cours
- affichage du titre, du visuel, de la console, du genre, de la date de sortie et du développeur
- affichage des informations du jeu en capsules recentrées
- bouton `Recharger` pour forcer un rechargement du jeu courant
- bouton `Rejouer` pour relancer un jeu quand le contexte local est fiable
- bouton `Passer` pour repousser localement un rétrosuccès non débloqué
- affichage d'un rétrosuccès mis en avant
- grille complète des rétrosuccès du jeu
- ordre de grille `Normal`, `Aléatoire`, `Facile`, `Difficile`
- mémorisation du mode de grille choisi
- retour automatique sur le premier rétrosuccès encore non débloqué
- distinction visuelle `Softcore` / `Hardcore`
- contour doré et halo léger pour les badges `Hardcore`
- légende compacte `Softcore` / `Hardcore` dans la section `Rétrosuccès du jeu`
- résumé de progression en points sous la forme `X / Total en softcore - Y / Total en hardcore`
- indicateur discret `Synchronisation...`
- restauration locale au démarrage
- aide utilisateur via `INSTRUCTION.md`
- mise en avant du bouton `Aide` à la première utilisation
- assistant OBS intégré dans `Aide`
- export OBS avec `index.html`, `overlay.css`, `overlay.js`, `state.json` et `layout.json`
- overlay OBS avec sections ajustables, liens par section et sauvegarde de layout
- rendu OBS différencié `Softcore` / `Hardcore` avec emblème hardcore et dégradés dorés
- hiérarchie visuelle retravaillée entre la carte principale et les sous-sections
- simplification visuelle de la zone `Progression`

## Historique par version

### 1.1

- version applicative du projet passée à `1.1`
- release `1.1` préparée pour publication
- manifeste `update.json` mis à jour pour pointer vers l'archive `RA.Compagnon-win-x64-1.1.zip`
- ajout d'un overlay OBS structuré en blocs : en-tête, progression, rétrosuccès en cours et grille des rétrosuccès
- séparation des fichiers OBS en `index.html`, `overlay.scss`, `normalize.scss` et `overlay.js`
- génération de `overlay.css` depuis les fichiers SCSS lors de l'export OBS
- ajout de liens OBS par section avec dimensions dans la prévisualisation
- sauvegarde de la taille et de la position des sections OBS dans `layout.json`
- ajout de la réinitialisation du layout OBS depuis l'aide
- ajout d'un assistant OBS dans la modale `Aide` pour préparer, vérifier et dépanner l'overlay
- ajout de la vérification des fichiers OBS requis : `state.json`, `layout.json`, `index.html`, `overlay.css`, `overlay.js`, `succes-embleme.png` et `succes-embleme-hardcore.png`
- ajout d'un état de test OBS depuis la section `OBS` de l'aide
- ajout de l'emblème de succès en arrière-plan de la section OBS `Rétrosuccès en cours`
- ajout d'un emblème hardcore distinct et de dégradés dorés quand le rétrosuccès courant est réussi en `Hardcore`
- retour automatique au rendu normal OBS lorsque le rétrosuccès courant n'est pas `Hardcore`
- amélioration des dégradés de l'emblème de succès en mode normal
- retrait des ombres portées dans l'overlay OBS pour un rendu plus propre dans les sources navigateur
- ajout de règles CSS explicites pour fiabiliser l'affichage de l'emblème hardcore dans OBS
- copie de `succes-embleme-hardcore.png` dans les sorties de build et les exports OBS
- renforcement de l'écriture OBS avec verrou partagé, remplacement atomique et reprises courtes si un fichier est lu en même temps
- ajout d'une checklist séparée `TODO-Validation-release.md`
- mise à jour de `INSTRUCTION.md` avec l'assistant OBS, les fichiers générés et le dépannage de l'overlay
- recalcul de faisabilité des rétrosuccès avec score hybride : rareté globale, indice hardcore, structure du succès, progression utilisateur, voisinage et Rich Presence
- archive de release `dist/RA.Compagnon-win-x64-1.1.zip` générée

### 1.0.9

- version applicative du projet passée à `1.0.9`
- manifeste `update.json` préparé pour la release `1.0.9`
- amélioration de la modale `Aide` : contenu simplifié, accordéons stabilisés, défilement unique et hauteur mieux adaptée à la carte principale
- ajout du bouton `Jouer` pour ouvrir `BizHawk` quand `Rejouer` n'a pas encore de chemin de jeu local fiable
- affichage des noms de fichiers compatibles entre `Jouer` et `Détails`
- retrait du module de fichiers compatibles dans la modale `Détails`
- amélioration de la détection et de l'affichage des groupes de rétrosuccès liés au succès courant
- ajustement de la barre de progression avec l'accent information vert `#43a82a`
- remplacement progressif des couleurs codées en dur par des ressources de thème centralisées
- ajustements visuels des boutons, des onglets, de la modale `Aide` et de la carte `Jeu en cours`
- correction de mojibakés et amélioration continue du français visible
- stabilité accrue après synchronisation et après déblocage local simulé d'un rétrosuccès
- archive de release `dist/RA.Compagnon-win-x64-1.0.9.zip` générée

### 1.0.8

- poursuite du polissage visuel de l'interface WPF
- restructuration des informations du jeu en capsules plus lisibles
- amélioration de l'affichage `Softcore` / `Hardcore` dans la grille et dans le rétrosuccès mis en avant
- affinage de la progression textuelle et de la légende associée
- harmonisation du français et des accents dans la documentation
- ajout de commentaires explicatifs en français dans les fichiers C# du projet
- relecture ciblée du code pour repérer les éléments manifestement inutiles
- restauration plus fiable de la taille et de la position de la fenêtre
- détection améliorée de `RetroArch` en version portable et installable
- mise en avant du bouton `Aide` à la première utilisation avec halo, contour doré et simulation de test en `DEBUG`
- archive de release `dist/RA.Compagnon-win-x64-1.0.8.zip` générée

### 1.0.7

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

## État du code

- commentaires explicatifs ajoutés en français dans les fichiers C# du projet
- français et accents harmonisés dans la documentation visible
- passe de relecture effectuée sur le code source pour repérer le code manifestement inutile
- garde-fou de build ajouté pour valider les noms XAML critiques avant compilation
- tâches asynchrones et minuteries sensibles sécurisées avec journalisation non bloquante
- export OBS renforcé contre les écritures concurrentes et les lectures simultanées

## Validation émulateurs

Validés et testés à ce stade :

- `RetroArch`
- `RALibretro`
- `Flycast`
- `DuckStation`
- `PCSX2`
- `PPSSPP`
- `BizHawk`
- `Dolphin`
- `RANes`
- `RAVBA`
- `RASnes9x`
- `RAP64`

Notes ciblées :

- `RetroArch` : prise en charge de la version portable et de la version installable

Non retenu :

- `LunaProject64`

## Fichiers de release à surveiller

- `RA.Compagnon/RA.Compagnon.csproj`
- `update.json`
- `README.md`
- `INSTRUCTION.md`
- `TODO-Validation-release.md`

## Rappel

Le numéro de version applicative, le nom de l'archive publiée et le manifeste `update.json` doivent être relus ensemble avant une release.
