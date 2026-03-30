# Checklist de validation

## Avant commit

- [x] Lancer les tests cibles :
  `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`
- [x] Verifier la build developpeur :
  `dotnet build RA.Compagnon.sln -m:1`
- [x] Verifier qu'aucune regression evidente n'apparait sur le changement de jeu.
- [x] Verifier qu'un changement de `GameID` reste correct sur au moins un emulateur local concerne si le commit touche la detection locale.
- [x] Verifier qu'un succes local reste bien detecte et affiche si le commit touche la chaine de detection de succes.
- [x] Verifier que la liste des retrosucces se charge completement si le commit touche le pipeline de jeu, le layout ou les animations.
- [x] Verifier que le mode diagnostic n'est pas laisse active par defaut.
- [x] Relire les fichiers modifies pour supprimer les traces de debug temporaires inutiles.

## Avant release

- [x] Rejouer les tests cibles :
  `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`
- [x] Generer le livrable :
  `./build.ps1`
- [x] Verifier que `dist/RA.Compagnon` est bien mis a jour.
- [x] Verifier un parcours manuel minimal :
  lancement de l'application, chargement du compte, affichage du dernier jeu, affichage de la liste des retrosucces.
- [x] Verifier au moins un changement de jeu reel sur une famille `log` :
  `DuckStation`, `PCSX2` ou `PPSSPP`.
- [x] Verifier au moins un changement de jeu reel sur une famille `RACache` :
  `RALibretro` ou `Luna's Project64`.
- [x] Verifier au moins un cas de detection de succes local si la release touche cette chaine.
- [x] Verifier que les fichiers `%AppData%\\RA-Compagnon` inutiles au livrable ne sont pas pris pour une source de validation automatique.
- [x] Noter brievement ce qui a ete valide si la release comporte des changements sensibles sur la detection locale, le pipeline de jeu ou la liste des succes.

## Validation actuelle

- Tests cibles OK via `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1`.
- Build developpeur OK via `dotnet build RA.Compagnon.sln -m:1`.
- Build livrable OK via `./build.ps1`.
- Changement de jeu valide sur familles `log` et `RACache`.
- Changement de `GameID` valide sur emulateurs locaux testes.
- Detection et affichage d'un succes local valides.
- Chargement complet de la liste des retrosucces valide.
- Mode diagnostic inactif par defaut sur cette session :
  variable `RA_COMPAGNON_DIAGNOSTIC` absente et fichier `diagnostic.enabled` absent.
- Relecture ciblee des fichiers modifies :
  pas de trace de debug temporaire involontaire detectee ;
  les boites de dialogue et journaux restants sont soit conditionnes au mode diagnostic,
  soit limites au fichier `MainWindow.DebugTests.cs` en `#if DEBUG`.
- Verification `%AppData%\\RA-Compagnon` :
  des journaux et caches runtime sont bien presents localement,
  mais ils restent hors livrable et ne constituent pas une validation automatique a eux seuls.

## Ordre conseille pour la validation manuelle

1. Lancer `RA.Compagnon` et verifier le parcours minimal :
   chargement du compte, affichage du dernier jeu, affichage de la liste des retrosucces.
2. Tester un changement de jeu sur une famille `log` :
   `DuckStation`, `PCSX2` ou `PPSSPP`.
3. Tester un changement de jeu sur une famille `RACache` :
   `RALibretro` ou `Luna's Project64`.
4. Si les changements touchent la chaine de succes locaux, declencher un succes reel
   ou un test debug `Ctrl+Shift+F9` sur une famille compatible.
5. Si les changements touchent surtout l'UI, verifier aussi un jeu avec peu de succes
   puis un jeu avec beaucoup de succes pour confirmer la liste et l'autodefilement.

## Resultats a noter

- Parcours minimal : OK
- Famille `log` testee : OK
- Famille `RACache` testee : OK
- Detection de succes local : OK
- Remarques UI / liste :
