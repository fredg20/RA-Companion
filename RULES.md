# Rules

## Ligne du projet

- `RA-Compagnon` reste une application `WPF` Windows.
- L'objectif principal est la clarté d'usage, pas l'accumulation de fonctions.
- Toute nouvelle UI doit rester cohérente avec la fenêtre actuelle et le vocabulaire français du projet.

## Règles de travail

- fermer `RA.Compagnon.exe` avant un build local
- garder le code C# formaté avec `CSharpier`
- préférer des changements ciblés et lisibles plutôt que des refontes diffuses
- ne pas réintroduire `Avalonia`
- préserver la persistance locale existante quand on modifie l'affichage
- éviter les régressions sur `Rejouer`, `Recharger`, `Passer` et la synchronisation

## Règles UI

- conserver les intitulés français visibles par l'utilisateur
- garder une hiérarchie visuelle simple : en-tête, cartes, actions
- éviter les marges gratuites et les espaces morts
- vérifier l'affichage en largeur normale et en plein écran
- ne pas dupliquer la même information à plusieurs endroits sans raison

## Règles release

- relire ensemble `RA.Compagnon.csproj`, `update.json` et `VERSION.md`
- vérifier le numéro de version avant toute publication
- vérifier le nom exact de l'archive publiée dans `dist`
- relire les notes de release visibles côté utilisateur

## Règles documentation

- `README.md` : vue d'ensemble du projet
- `INSTRUCTION.md` : manuel utilisateur
- `VERSION.md` : état de version et contenu livré
- `TODO-Fonctionnalites.md` : feuille de route simplifiée
- `API_RETROACHIEVEMENTS.md` : endpoints RA utilisés par l'application
