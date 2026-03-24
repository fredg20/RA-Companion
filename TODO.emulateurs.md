# TODO émulateurs

Feuille de route de support émulateurs pour `Measured` / `rcheevos`.

## Priorités

- consolider `RetroArch` comme source live principale
- ajouter un support dédié pour `PPSSPP`
- ajouter un support dédié pour `BizHawk`
- étudier un support dédié pour `Luna's Project64`
- conserver `RALibRetro` comme fallback passif via les `.state`
- étudier ensuite `Flycast`, puis `PCSX2`, `Dolphin` et `DuckStation`

## Notes

- privilégier les interfaces officielles ou stables quand elles existent
- éviter la lecture mémoire brute du process comme solution principale
- garder une architecture hybride
- source live standardisée
- adaptateurs par émulateur
- fallback passif via savestates
