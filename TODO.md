# TODO Robustesse RA-Companion

## 1. Orchestrateur d'état
- [x] Mettre en place une vraie machine d'états pour le flux `aucun jeu -> détection locale -> jeu confirmé -> chargement -> affichage`.
- [x] Distinguer clairement les états transitoires des états réellement affichables.
- [x] Empêcher qu'une source tardive mais moins fiable écrase un état déjà validé.
- [x] Centraliser les délais de grâce, les mécanismes anti-rebond et le dédoublonnage.

## 2. Couche unifiée des émulateurs
- [x] Définir un contrat commun par émulateur : process, titre, `GameID`, succès locaux et fraîcheur des données.
- [x] Factoriser la lecture des journaux et des caches : partage de lecture, verrous, horodatage et solution de repli.
- [x] Normaliser les diagnostics des sources locales.
- [x] Réduire la logique spécifique aujourd'hui dispersée entre plusieurs classes.
- [x] Revoir la détection du `GameID` pour `PCSX2` et `PPSSPP` sur le modèle de `DuckStation`, en s'appuyant sur leur journal local plutôt que sur une déduction fragile.

## 3. Pipeline de chargement du jeu
- [x] Distinguer clairement les données minimales, les métadonnées enrichies, la liste des succès et les images.
- [x] Empêcher qu'une liste partielle remplace une liste complète.
- [x] Rendre le chargement idempotent pour un même jeu.
- [x] Ajouter des garde-fous pour les changements rapides de jeu.

## 4. Stabilisation UI et animations
- [x] Isoler la logique de la liste des rétrosuccès dans un composant dédié.
- [x] Séparer explicitement les états `AutoScroll`, `PauseSurvol` et `InteractionManuelle`.
- [x] Limiter les recalculs de layout concurrents.
- [x] Simplifier la reprise de l'animation pour supprimer les sauts de position.

## 5. Diagnostic, tests et mode debug
- [x] Conserver les journaux dans un mode diagnostic activable, plutôt qu'en permanence.
- [x] Ajouter des tests ciblés pour les changements de jeu, le `GameID`, les succès détectés localement et les listes longues.
- [x] Étendre le débloqueur virtuel pour couvrir chaque famille d'émulateurs.
- [x] Maintenir une checklist de validation avant commit et avant release.
