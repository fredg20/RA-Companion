# TODO Problèmes Importants

## Critique

- [x] Corriger l'instabilité de démarrage de `Compagnon`, surtout quand un émulateur est déjà ouvert.
- [x] Isoler précisément le blocage actuel au chargement de `MainWindow`.
- [x] Garantir qu'une seule instance utilisable de `Compagnon` reste active au démarrage.

## Constat actuel

- [x] `InitializeComponent()` et `MainWindow ctor` ne sont plus le point de blocage principal.
- [x] Le temps de chargement restant vient surtout de `FenetrePrincipaleChargee`.
- [x] Les sous-étapes les plus coûteuses observées sont :
  - `AppliquerDernierJeuSauvegardeAsync`
  - `AppliquerProfilUtilisateurAsync`

## Élevé

- [x] Stabiliser la sonde live `RetroArch` pour éviter tout gel ou faux état.
- [x] Éviter que la détection locale bloque l'affichage initial de la fenêtre.
- [x] Vérifier que `GET_STATUS` n'introduit pas de blocage UI.
- [x] Nettoyer les diagnostics temporaires avant release si non nécessaires.

## Moyen

- [ ] Vérifier la cohérence entre `game.json`, `achievement.json` et `achievements_list.json`.
- [ ] Revalider le flux `Measured` sur plusieurs jeux réels.
- [ ] Revalider le comportement de la grille après les dernières modifications UI.
- [ ] Réduire la dette technique autour de `MainWindow.xaml.cs`.

## À surveiller

- [ ] Régressions visuelles après simplification ou rollback de XAML.
- [ ] Écarts entre le build `Debug` et le livrable `dist/RA.Compagnon`.
- [ ] Logs temporaires laissés actifs trop longtemps.
