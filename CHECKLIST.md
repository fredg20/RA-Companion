# Checklist de validation

## Avant commit

- [x] Lancer les tests ciblés :
  `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`
- [x] Vérifier la build développeur :
  `dotnet build RA.Compagnon.sln -m:1`
- [x] Vérifier qu'aucune régression évidente n'apparaît sur le changement de jeu.
- [x] Vérifier qu'un changement de `GameID` reste correct sur au moins un émulateur local concerné si le commit touche la détection locale.
- [x] Vérifier qu'un succès local reste bien détecté et affiché si le commit touche la chaîne de détection de succès.
- [x] Vérifier que la liste des rétrosuccès se charge complètement si le commit touche le pipeline de jeu, le layout ou les animations.
- [x] Vérifier que le mode diagnostic n'est pas laissé actif par défaut.
- [x] Relire les fichiers modifiés pour supprimer les traces de debug temporaires inutiles.

## Avant release

- [x] Rejouer les tests ciblés :
  `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`
- [x] Générer le livrable :
  `./build.ps1`
- [x] Vérifier que `dist/RA.Compagnon` est bien mis à jour.
- [x] Vérifier un parcours manuel minimal :
  lancement de l'application, chargement du compte, affichage du dernier jeu, affichage de la liste des rétrosuccès.
- [x] Vérifier au moins un changement de jeu réel sur une famille `log` :
  `DuckStation`, `PCSX2` ou `PPSSPP`.
- [x] Vérifier au moins un changement de jeu réel sur une famille `RACache` :
  `RALibretro` ou `Luna's Project64`.
- [x] Vérifier au moins un cas de détection de succès local si la release touche cette chaîne.
- [x] Vérifier que les fichiers `%AppData%\RA-Compagnon` inutiles au livrable ne sont pas pris pour une source de validation automatique.
- [x] Noter brièvement ce qui a été validé si la release comporte des changements sensibles sur la détection locale, le pipeline de jeu ou la liste des succès.

## Validation actuelle

- Tests ciblés OK via `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`.
- Build développeur OK via `dotnet build RA.Compagnon.sln -m:1`.
- Build livrable OK via `./build.ps1`.
- Changement de jeu validé sur les familles `log` et `RACache`.
- Changement de `GameID` validé sur les émulateurs locaux testés.
- Détection et affichage d'un succès local validés.
- Chargement complet de la liste des rétrosuccès validé.
- Mode diagnostic inactif par défaut sur cette session :
  variable `RA_COMPAGNON_DIAGNOSTIC` absente et fichier `diagnostic.enabled` absent.
- Relecture ciblée des fichiers modifiés :
  pas de trace de debug temporaire involontaire détectée ;
  les boîtes de dialogue et journaux restants sont soit conditionnés au mode diagnostic,
  soit limités au fichier `MainWindow.DebugTests.cs` en `#if DEBUG`.
- Vérification `%AppData%\RA-Compagnon` :
  des journaux et caches runtime sont bien présents localement,
  mais ils restent hors livrable et ne constituent pas une validation automatique à eux seuls.

## Ordre conseillé pour la validation manuelle

1. Lancer `RA.Compagnon` et vérifier le parcours minimal :
   chargement du compte, affichage du dernier jeu, affichage de la liste des rétrosuccès.
2. Tester un changement de jeu sur une famille `log` :
   `DuckStation`, `PCSX2` ou `PPSSPP`.
3. Tester un changement de jeu sur une famille `RACache` :
   `RALibretro` ou `Luna's Project64`.
4. Si les changements touchent la chaîne de succès locaux, déclencher un succès réel
   ou un test debug `Ctrl+Shift+F9` sur une famille compatible.
5. Si les changements touchent surtout l'UI, vérifier aussi un jeu avec peu de succès
   puis un jeu avec beaucoup de succès pour confirmer la liste et l'autodéfilement.

## Résultats à noter

- Parcours minimal : OK
- Famille `log` testée : OK
- Famille `RACache` testée : OK
- Détection de succès local : OK
- Remarques UI / liste :
