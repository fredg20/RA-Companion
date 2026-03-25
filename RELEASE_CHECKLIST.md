# Release Checklist

## Objectif

Préparer une release stable de `Compagnon` pour vendredi, sans ajouter de gros chantier de dernière minute.

## Avant jeudi soir

- Vérifier le changement de jeu sur les émulateurs déjà visés.
- Valider que `Rétrosuccès en cours` suit bien la grille.
- Valider les clics gauche et droit sur les badges.
- Vérifier que `Measured` n'apparaît que lorsqu'il existe vraiment.
- Vérifier la persistance de :
  - `user.json`
  - `game.json`
  - `achievement.json`
  - `achievements_list.json`
- Vérifier que la jaquette, la transition et la grille restent stables visuellement.

## Jeudi

- Ne plus ajouter de grosse fonctionnalité.
- Corriger seulement les bugs reproductibles.
- Retirer ou désactiver les diagnostics temporaires encore présents si la release ne doit pas les inclure.
- Relire `README.md`.
- Vérifier `.gitignore`.
- Produire un build Release propre.

## Vendredi avant release

- Lancer l'application depuis un état propre.
- Tester un démarrage complet.
- Tester un changement réel de jeu.
- Tester un cas avec rétrosuccès non débloqués.
- Tester un cas avec rétrosuccès débloqués.
- Tester un clic gauche sur la grille.
- Tester un clic droit sur la grille.
- Vérifier que `dist/RA.Compagnon` contient bien tout le nécessaire.

## Commandes de vérification

```powershell
dotnet build RA.Compagnon.sln -m:1
./build.ps1
```

## Go / No-Go

### Go

- Build propre.
- Pas de crash.
- Changement de jeu acceptable.
- UI cohérente.
- Persistance locale fonctionnelle.

### No-Go

- Crash au changement de jeu.
- Persistance cassée.
- `Rétrosuccès en cours` incohérent.
- Build instable.
