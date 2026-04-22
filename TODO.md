# TODO

Feuille de route de `Compagnon`.

Ce fichier regroupe les évolutions possibles du projet, les améliorations de confort, les chantiers techniques et les idées de polissage encore ouvertes.

## Priorité immédiate

- [ ] Valider la modale `Aide` sur toutes les largeurs de fenêtre
- [ ] Vérifier que tous les comportements d'accordéon restent cohérents dans `Aide`
- [ ] Repasser sur les zones de défilement pour éviter toute régression de double `ScrollViewer`
- [ ] Continuer le nettoyage de `MainWindow` en petits blocs plus lisibles
- [ ] Revoir les points de fragilité liés aux builds WPF temporaires
- [ ] Relire les signatures de méthodes UI pour éviter les incohérences entre fichiers partiels
- [ ] Consolider la persistance locale du compte et du dernier état restauré
- [ ] Ajouter une passe de validation complète avant chaque release

## Interface principale

- [ ] Continuer le polissage visuel global de la fenêtre principale
- [ ] Harmoniser encore les espacements entre en-tête, cartes et barre d'état
- [ ] Uniformiser définitivement les tailles de boutons dans toute l'application
- [ ] Réviser l'alignement vertical de tous les libellés de section
- [ ] Améliorer l'équilibre visuel entre la carte `Jeu en cours` et les deux sections de rétrosuccès
- [ ] Prévoir un mode plus compact pour les petites largeurs de fenêtre
- [ ] Revoir la hiérarchie visuelle des sous-informations du jeu
- [ ] Affiner l'affichage des capsules d'information du jeu selon la largeur disponible
- [ ] Mieux gérer les chaînes très longues pour les titres de jeu
- [ ] Éviter tout saut visuel lorsque le jeu courant change
- [ ] Revoir l'affichage quand aucune image de jeu n'est disponible
- [ ] Ajouter un état visuel plus clair quand aucune donnée de jeu n'est encore chargée
- [ ] Ajouter un meilleur état vide quand aucun rétrosuccès n'est exploitable

## Carte `Jeu en cours`

- [ ] Ajouter une vue plus détaillée des informations du jeu sans surcharger la carte principale
- [ ] Enrichir la modale `Détails` avec davantage d'informations utiles
- [ ] Ajouter le nombre total de joueurs du jeu quand l'information est disponible
- [ ] Ajouter un libellé plus explicite pour les jeux sans développeur connu
- [ ] Prévoir un meilleur fallback quand le genre ou la date sont absents
- [ ] Étudier l'ajout d'un indicateur local fiable pour le mode `Softcore` ou `Hardcore`
- [ ] Améliorer la lisibilité des points et de la progression du jeu
- [ ] Ajouter une action rapide pour ouvrir la page RetroAchievements du jeu
- [ ] Ajouter un indicateur plus clair quand `Rejouer` est indisponible
- [ ] Ajouter une explication plus précise de l'état grisé du bouton `Rejouer`

## Rétrosuccès en cours

- [ ] Améliorer encore la fiche du succès mis en avant
- [ ] Revoir la mise en page des points, rétro points et faisabilité
- [ ] Ajouter un affichage plus explicite des informations `Softcore` / `Hardcore`
- [ ] Mieux distinguer visuellement un succès déjà obtenu d'un succès encore à faire
- [ ] Ajouter une meilleure indication quand le succès affiché est temporairement épinglé
- [ ] Ajouter un moyen clair de revenir au premier succès non débloqué
- [ ] Affiner le comportement du bouton `Passer`
- [ ] Ajouter une option pour annuler le dernier `Passer`
- [ ] Ajouter une logique de remise à zéro locale de l'ordre personnalisé des succès non débloqués
- [ ] Étudier un affichage plus pédagogique de la faisabilité

## Grille des rétrosuccès

- [ ] Ajouter une option de tri par points
- [ ] Ajouter une option de tri par rareté
- [ ] Ajouter une option de tri par ordre officiel RetroAchievements
- [ ] Ajouter un filtre pour n'afficher que les succès non débloqués
- [ ] Ajouter un filtre pour n'afficher que les succès débloqués
- [ ] Ajouter un filtre `Softcore` / `Hardcore`
- [ ] Ajouter un filtre pour les succès manqués ou potentiellement manquables
- [ ] Ajouter une recherche textuelle dans la grille
- [ ] Ajouter une option pour afficher ou masquer les badges déjà obtenus
- [ ] Améliorer encore la légende visuelle `Softcore` / `Hardcore`
- [ ] Ajouter une infobulle plus riche pour chaque badge
- [ ] Ajouter une option pour épingler durablement un succès dans la carte principale
- [ ] Ajouter une navigation clavier dans la grille
- [ ] Ajouter un focus visuel clair pour la navigation clavier

## Progression et statistiques

- [ ] Revoir la présentation de la progression `succès` et `points`
- [ ] Ajouter une lecture plus intuitive des points `Softcore` et `Hardcore`
- [ ] Ajouter le nombre de succès obtenus sur le total du jeu dans un format plus visible
- [ ] Ajouter une synthèse rapide `reste à faire`
- [ ] Ajouter une estimation du temps ou de l'effort restant, si un modèle local devient pertinent
- [ ] Affiner encore le calcul de faisabilité des rétrosuccès
- [ ] Ajouter une explication détaillée du calcul de faisabilité dans l'interface
- [ ] Prévoir un mode de faisabilité simplifié et un mode détaillé
- [ ] Ajouter des couleurs plus parlantes pour les niveaux de faisabilité
- [ ] Permettre de masquer la faisabilité pour les joueurs qui ne la souhaitent pas

## Compte utilisateur

- [ ] Revoir la modale `Compte` pour qu'elle soit aussi responsive que la modale `Aide`
- [ ] Ajouter un état plus explicite quand le compte n'est pas encore configuré
- [ ] Ajouter une vérification plus rassurante après enregistrement des identifiants
- [ ] Mieux guider l'utilisateur si la clé API est invalide
- [ ] Ajouter une action pour tester la connexion sans fermer la modale
- [ ] Ajouter une meilleure visibilité sur le pseudo, les points et les récompenses du compte
- [ ] Ajouter une meilleure gestion des avatars absents ou invalides

## Aide et documentation intégrée

- [ ] Continuer à améliorer la modale `Aide`
- [ ] Ajouter davantage de sections d'aide orientées diagnostic
- [ ] Ajouter une section `Questions fréquentes`
- [ ] Ajouter une section `Que faire si le jeu ne change pas ?`
- [ ] Ajouter une section `Que faire si un succès n'apparaît pas ?`
- [ ] Ajouter une section `Comment fonctionne Rejouer ?`
- [ ] Ajouter une section `Comment fonctionne Recharger ?`
- [ ] Ajouter une section `Comprendre Softcore et Hardcore`
- [ ] Ajouter une section `Comprendre la faisabilité`
- [ ] Permettre d'ouvrir `INSTRUCTION.md` directement depuis plus d'endroits de l'interface
- [ ] Harmoniser complètement le ton et le vocabulaire entre `Aide`, `README` et `INSTRUCTION`

## Détection locale et émulateurs

- [ ] Continuer l'amélioration de la détection locale du jeu en cours
- [ ] Renforcer la fiabilité de détection quand plusieurs émulateurs sont ouverts
- [ ] Mieux gérer les changements rapides de jeu ou de fenêtre
- [ ] Ajouter plus d'informations de diagnostic pour chaque émulateur reconnu
- [ ] Améliorer les messages quand un chemin attendu n'est pas trouvé
- [ ] Ajouter une meilleure gestion des installations multiples d'un même émulateur
- [ ] Mieux distinguer installation portable et installation classique dans les diagnostics
- [ ] Ajouter un résumé global de confiance de détection par émulateur
- [ ] Mémoriser plus intelligemment les emplacements manuels choisis par l'utilisateur
- [ ] Ajouter une action pour revalider manuellement un émulateur configuré
- [ ] Ajouter un indicateur de fraîcheur des journaux locaux utilisés
- [ ] Étudier la prise en charge de nouveaux émulateurs compatibles avec la logique du projet
- [ ] Étudier `SkyEmu` comme source potentielle pour une future prise en charge : https://github.com/skylersaleh/SkyEmu
- [ ] Étudier `Firelight` comme source potentielle pour une future prise en charge : https://github.com/firelight-emulator/firelight
- [ ] Ajouter une validation fonctionnelle plus poussée pour chaque émulateur déjà supporté

## `Rejouer`

- [ ] Continuer la fiabilisation de `Rejouer` selon les émulateurs
- [ ] Ajouter une meilleure explication lorsque la relance n'est pas possible
- [ ] Ajouter un diagnostic local de la commande réellement utilisée pour `Rejouer`
- [ ] Ajouter un mode de test de `Rejouer` dans l'aide ou en diagnostic avancé
- [ ] Mieux gérer les chemins contenant des caractères spéciaux
- [ ] Ajouter une meilleure gestion des jeux multi-disques si le besoin apparaît
- [ ] Prévoir une validation plus stricte avant d'autoriser la relance

## Synchronisation et chargement

- [ ] Rendre la synchronisation encore plus fluide visuellement
- [ ] Réduire les gels perçus pendant les rafraîchissements
- [ ] Mieux différencier la restauration locale du rafraîchissement réseau réel
- [ ] Ajouter un état plus précis quand seule la couche locale répond
- [ ] Ajouter une protection contre les rechargements redondants
- [ ] Ajouter un meilleur journal de synchronisation pour le diagnostic
- [ ] Revoir le cycle de chargement au démarrage pour qu'il soit plus lisible
- [ ] Ajouter une stratégie de repli plus claire si l'API répond lentement

## Persistance locale

- [ ] Continuer à sécuriser l'écriture des fichiers locaux
- [ ] Ajouter des sauvegardes temporaires ou atomiques lors des écritures sensibles
- [ ] Vérifier la cohérence croisée entre `user.json`, `game.json` et `achievement.json`
- [ ] Ajouter une validation des données locales au chargement
- [ ] Ajouter une récupération plus propre en cas de fichier local corrompu
- [ ] Ajouter une journalisation claire des restaurations locales
- [ ] Permettre une remise à zéro locale contrôlée des données mises en cache

## Performances

- [ ] Mesurer les temps de chargement réels des principales actions
- [ ] Réduire le coût des recalculs de layout sur les grandes fenêtres
- [ ] Réduire le coût des rechargements de la grille complète des succès
- [ ] Optimiser la création des badges et des infobulles
- [ ] Limiter les travaux UI inutiles quand aucune donnée n'a changé
- [ ] Revoir les minuteurs pour éviter les rafraîchissements trop fréquents
- [ ] Ajouter quelques diagnostics de performance désactivables

## Architecture et code

- [ ] Continuer le découpage de `MainWindow` en responsabilités plus claires
- [ ] Monter progressivement davantage d'état vers la couche `ViewModel`
- [ ] Réduire encore les accès directs à l'UI depuis la logique métier
- [ ] Isoler davantage les helpers de diagnostic, de layout et de modales
- [ ] Revoir les méthodes trop longues et les découper
- [ ] Réduire les duplications entre sections d'aide et de compte
- [ ] Continuer le nettoyage des noms de méthodes, variables et régions logiques
- [ ] Consolider les constantes visuelles dans `ConstantesDesign`
- [ ] Revoir les dépendances implicites entre fichiers partiels de `MainWindow`
- [ ] Stabiliser les points sensibles liés au compilateur WPF temporaire

## Tests et validation

- [ ] Ajouter plus de tests unitaires sur les services purs
- [ ] Ajouter des tests sur les calculs de progression
- [ ] Ajouter des tests sur la logique `Softcore` / `Hardcore`
- [ ] Ajouter des tests sur la faisabilité des succès
- [ ] Ajouter des tests sur la persistance locale
- [ ] Ajouter des scénarios de validation manuelle par émulateur
- [ ] Créer une checklist de non-régression avant release
- [ ] Ajouter des scénarios de test pour les fenêtres petites, moyennes et larges

## Accessibilité et confort

- [ ] Ajouter davantage d'infobulles utiles sans surcharger l'interface
- [ ] Vérifier la navigation clavier dans les modales
- [ ] Vérifier les contrastes de texte et de fond
- [ ] Mieux gérer les très longues chaînes ou les langues plus verbeuses
- [ ] Ajouter des raccourcis clavier documentés pour les actions principales
- [ ] Ajouter une option pour réduire certaines animations
- [ ] Ajouter une option pour désactiver certains effets visuels

## Préparatifs d'intégration OBS

### Fondation technique OBS

- [x] Définir les informations utiles à afficher dans OBS : jeu courant, progression, succès en cours, dernier succès obtenu
- [x] Étudier une sortie locale simple pour OBS, par exemple fichier JSON, fichier texte ou page HTML locale
- [x] Choisir le format principal de sortie : `overlay.html`, `state.json`, fichiers `.txt`, images locales ou combinaison de ces formats
- [x] Définir le dossier stable des fichiers OBS, par exemple `%AppData%\RA-Compagnon\OBS`
- [x] Définir le modèle de données minimal : jeu, console, progression, badge, succès, description, points, mode `Softcore` / `Hardcore`
- [x] Écrire les fichiers OBS de façon atomique pour éviter qu'OBS lise un fichier incomplet
- [x] Prévoir des fichiers séparés pour les sources OBS : titre du jeu, progression, badge, description, état de synchronisation
- [x] Servir `overlay.html` et `state.json` par une URL locale pour éviter les blocages de lecture de fichiers locaux dans OBS

### Contrôle utilisateur et confidentialité

- [x] Ajouter une option d'activation ou de désactivation de l'export OBS dans `Compagnon`
- [ ] Ajouter une option pour masquer les informations sensibles du compte dans les sorties OBS
- [ ] Ajouter un mode `streamer` qui masque le pseudo, les données de compte et toute information personnelle non nécessaire

### Robustesse et états limites

- [ ] Prévoir les états vides : aucun jeu chargé, aucune progression, aucun succès en cours, synchronisation en attente
- [ ] Prévoir les états d'erreur : API indisponible, émulateur fermé, jeu inconnu, image manquante, succès non chargé
- [ ] Étudier la fréquence de mise à jour idéale pour éviter les écritures inutiles et les saccades

### Images et cache local

- [ ] Prévoir la copie locale des images utiles, comme badge du succès courant et image du jeu, pour éviter les dépendances réseau en capture
- [ ] Prévoir un cache local des images OBS avec nettoyage contrôlé

### Design des overlays

- [ ] Prévoir un mode overlay compact distinct de l'interface principale
- [ ] Prévoir plusieurs modes OBS : overlay complet, bandeau compact, succès courant seulement, dernier succès obtenu, progression seule
- [ ] Prévoir un mode `intermission` ou pause avec seulement le jeu, la progression et le prochain objectif
- [ ] Prévoir un thème OBS séparé : police, tailles, couleurs, transparence, marges et dimensions de capture
- [ ] Ajouter une personnalisation minimale : thème clair ou sombre, couleur d'accent, taille de police, transparence et animations
- [ ] Définir un style visuel lisible en capture vidéo, avec contraste élevé et peu de bruit visuel
- [ ] Étudier une animation temporaire lors d'un succès obtenu

### Interface dans Compagnon

- [x] Ajouter un bouton pour ouvrir le dossier OBS depuis `Compagnon`
- [x] Ajouter un bouton pour copier l'URL locale de `overlay.html`
- [x] Ajouter un bouton pour copier le chemin de `state.json`
- [ ] Ajouter un aperçu OBS directement dans `Compagnon`
- [x] Ajouter un bouton de test pour générer un état OBS fictif

### Documentation et validation OBS

- [x] Définir une résolution recommandée pour l'overlay, par exemple 1920x1080 et variantes compactes
- [ ] Documenter l'ordre conseillé des calques OBS
- [ ] Documenter comment ajouter une source navigateur OBS pointant vers `overlay.html`
- [ ] Documenter comment ajouter des sources texte OBS pointant vers les fichiers `.txt`
- [ ] Tester le comportement quand OBS est ouvert pendant les écritures
- [ ] Tester le comportement quand OBS est fermé puis rouvert après plusieurs mises à jour
- [ ] Vérifier qu'aucune clé API, donnée sensible ou information privée non désirée n'est exportée
- [ ] Documenter un premier flux d'utilisation OBS dans `INSTRUCTION.md` lorsque l'intégration sera prête
- [ ] Ajouter une checklist de test avec OBS Studio avant toute publication de cette fonctionnalité

### Évolutions futures OBS

- [ ] Prévoir une stratégie pour ne pas télécharger d'images pendant une scène active
- [ ] Étudier une intégration future avec OBS WebSocket pour déclencher des scènes ou des animations

## Documentation et release

- [ ] Maintenir `README.md` à jour à chaque évolution visible
- [ ] Maintenir `INSTRUCTION.md` à jour à chaque changement de flux utilisateur
- [ ] Maintenir `VERSION.md` à jour à chaque étape importante
- [ ] Préparer une note de release propre pour `1.1`
- [ ] Garder `update.json` sur la dernière release prête tant que `1.1` n'est pas validée
- [ ] Vérifier ensemble la cohérence entre `VERSION`, `update.json` et l'archive publiée
- [ ] Ajouter une procédure de packaging plus explicite pour les releases portables
- [ ] Ajouter une checklist de publication du runtime .NET séparé
- [ ] Documenter clairement les prérequis utilisateur pour lancer `Compagnon`

## Idées à discuter ensemble

- [ ] Bouton `Réinitialiser localement` pour remettre l'état du jeu courant à zéro côté Compagnon
- [ ] Historique local des derniers jeux consultés
- [ ] Section dédiée aux succès récemment obtenus
- [ ] Vue spéciale pour les succès restants les plus accessibles
- [ ] Comparaison rapide entre progression `Softcore` et `Hardcore`
- [ ] Favoris ou épingles de jeux
- [ ] Vue compacte secondaire pour laisser `Compagnon` ouvert en parallèle d'un émulateur
- [ ] Mode diagnostic avancé activable à la demande
