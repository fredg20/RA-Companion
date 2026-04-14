# TODO Fonctionnalités RA-Compagnon

## État de référence

- version actuelle : `1.0.8`
- interface active : `WPF`
- release `1.0.8` considérée prête

## Déjà livré

- [x] afficher le dernier jeu joué
- [x] afficher le jeu en cours détecté localement
- [x] afficher une carte détaillée du jeu courant
- [x] afficher les informations du jeu en capsules lisibles
- [x] afficher la grille complète des rétrosuccès du jeu
- [x] mettre en avant un rétrosuccès courant
- [x] ajouter `Recharger`
- [x] ajouter `Rejouer`
- [x] ajouter `Passer`
- [x] mémoriser le mode de tri `Normal`, `Aléatoire`, `Facile`, `Difficile`
- [x] restaurer l'état local au démarrage
- [x] restaurer plus fiablement la taille et la position de la fenêtre
- [x] distinguer visuellement `Softcore` et `Hardcore`
- [x] ajouter la légende `Softcore` / `Hardcore`
- [x] mettre en avant le bouton `Aide` à la première utilisation
- [x] ajouter une aide utilisateur simple
- [x] valider la relance sur les émulateurs pris en charge
- [x] prendre en charge `RetroArch` en version portable et installable
- [x] estimer simplement la faisabilité des rétrosuccès

## Priorités suivantes

- [ ] rendre l'état des données plus explicite : local, restauré, synchronisé, en cours de rafraîchissement
- [ ] améliorer encore la lisibilité des cartes sur petites largeurs
- [ ] rendre le comportement de `Rejouer` encore plus robuste sur tous les émulateurs validés
- [ ] mieux distinguer visuellement les succès verrouillés, `Softcore` et `Hardcore`
- [ ] afficher plus clairement les succès restants et les points restants pour terminer un jeu
- [ ] affiner la hiérarchie visuelle de l'en-tête et de la barre d'état

## À moyen terme

- [ ] ajouter une recherche rapide par nom de jeu
- [ ] ajouter une recherche rapide par nom de succès
- [ ] ajouter un filtre des succès : tous, verrouillés, obtenus, softcore, hardcore
- [ ] ajouter une vue des jeux récemment joués
- [ ] ajouter une vue des jeux proches d'une complétion
- [ ] ajouter une fiche détaillée pour chaque succès
- [ ] ajouter un écran de préférences simple
- [ ] permettre de réduire certaines animations

## Idées à confirmer avant implémentation

- [ ] ajouter un indicateur visuel du mode de jeu local quand l'émulateur permet une déduction fiable
- [ ] ajouter une action de réinitialisation ou de resynchronisation avancée si l'API RetroAchievements le permet proprement
- [ ] enrichir la notice d'aide avec des diagnostics ciblés par émulateur

## À garder simples

- [ ] éviter toute logique fragile de lecture mémoire invasive
- [ ] éviter les fonctions sociales complexes intégrées à l'application
- [ ] éviter de transformer `Compagnon` en clone d'un gestionnaire RA plus lourd
