# ra_compagnon_rcheevos_bridge

Bridge natif prévu pour relier `RA.Compagnon` à `rcheevos`.

## But

Ce module a vocation à :

- recevoir le contexte du jeu actif
- recevoir un callback de lecture mémoire
- enregistrer les définitions `MemAddr` des succès
- interroger `rcheevos` sur le `Progress Indicator` d'un succès
- exposer des points d'entrée C simples consommables via `DllImport` côté C#

## État actuel

Le dossier contient maintenant un bridge natif compilable, capable de :

- enregistrer un lecteur mémoire fourni par `RA.Compagnon`
- enregistrer une progression sérialisée issue d'un savestate `RALibRetro`
- enregistrer les définitions de succès du jeu courant
- évaluer un `rc_runtime` réel si les sources `rcheevos` sont présentes
- exposer un `Progress Indicator` mesuré via `rc_runtime`

Sans dépôt `rcheevos` vendorisé, le bridge reste en mode squelette et renvoie simplement `indisponible`.

## Fonctions exportées actuelles

- `ra_compagnon_rcheevos_register_memory_reader`
- `ra_compagnon_rcheevos_clear_memory_reader`
- `ra_compagnon_rcheevos_probe_memory_reader`
- `ra_compagnon_rcheevos_clear_achievement_definitions`
- `ra_compagnon_rcheevos_set_serialized_progress`
- `ra_compagnon_rcheevos_clear_serialized_progress`
- `ra_compagnon_rcheevos_set_achievement_definition`
- `ra_compagnon_rcheevos_get_progress_indicator`

## Dépôt `rcheevos`

Le `CMakeLists.txt` cherche automatiquement un dépôt vendorisé dans l'un de ces emplacements :

- `native/ra_compagnon_rcheevos_bridge/third_party/rcheevos`
- `native/third_party/rcheevos`
- `third_party/rcheevos`

On peut aussi fournir explicitement un chemin :

```powershell
cmake -S native/ra_compagnon_rcheevos_bridge -B native/ra_compagnon_rcheevos_bridge/build -DRA_COMPAGNON_RCHEEVOS_DIR=C:\chemin\vers\rcheevos
```

Si `rcheevos` est détecté, le bridge ajoute automatiquement les sources `src/*.c` et compile avec `RA_COMPAGNON_HAS_RCHEEVOS=1`.

## Build local

Le build Zig local génère actuellement :

- `native/ra_compagnon_rcheevos_bridge/build-zig/ra_compagnon_rcheevos_bridge.dll`

Cette DLL est ensuite recopiée automatiquement dans les sorties .NET via `RA.Compagnon.csproj`.

## Étape suivante

La prochaine étape utile est de valider la chaîne complète avec un vrai savestate `RALibRetro` contenant un bloc `ACHV` :

- détection du `.state` cible
- extraction du bloc `ACHV`
- désérialisation runtime dans le bridge
- vérification réelle du `Progress Indicator` dans `Compagnon`
