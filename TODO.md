# TODO

## MVP - Connexion et configuration

- [x] Finaliser la modale de connexion comme point d'entrée unique de l'application.
- [x] Vérifier la sauvegarde locale du pseudo et de la clé API.
- [x] Valider la clé API et le pseudo avant d'enregistrer la configuration.
- [x] Permettre la reconnexion et la déconnexion sans incohérence d'état.
- [x] Empêcher le lancement du suivi automatique tant que la connexion n'est pas valide.
- [x] Afficher des erreurs claires si la configuration est invalide ou incomplète.
- [x] Afficher clairement l'état de connexion actuel : connecté, invalide, inaccessible ou hors ligne.
- [x] Permettre de modifier la connexion sans supprimer toute la configuration visuelle locale.
- [x] Masquer partiellement la clé API dans l'interface hors saisie.

## MVP - Profil utilisateur

- [x] Appeler l'API RetroAchievements après une connexion réussie.
- [x] Charger le profil de l'utilisateur surveillé.
- [x] Afficher les informations essentielles du profil.
- [x] Gérer le cas où l'utilisateur n'existe pas ou n'est pas accessible.

## MVP - Jeu en cours

- [x] Détecter le jeu actuellement joué par l'utilisateur.
- [x] Afficher le titre du jeu courant.
- [x] Afficher les métadonnées utiles du jeu courant.
- [x] Mettre à jour automatiquement l'affichage lorsque le jeu change.

## MVP - Suivi des succès en temps réel

- [x] Mettre en place une boucle de rafraîchissement périodique.
- [x] Récupérer les succès récemment débloqués.
- [ ] Dédupliquer les succès déjà vus localement.
- [x] Afficher les nouveaux succès dans l'ordre le plus utile.
- [x] Prévoir un état vide si aucun nouveau succès n'est détecté.

## MVP - Progression du jeu

- [x] Charger la progression utilisateur pour le jeu courant.
- [x] Afficher le nombre de succès obtenus.
- [x] Afficher le nombre total de succès du jeu.
- [x] Afficher un indicateur clair de progression.
- [x] Prévoir l'affichage du mode Hardcore si disponible.

## MVP - Interface de suivi

- [x] Remplacer les contenus de démonstration par de vraies données RetroAchievements.
- [x] Définir une hiérarchie visuelle claire entre profil, jeu courant et derniers succès.
- [ ] Prévoir des sections stables pour les futures vues du compagnon.
- [x] Vérifier que l'interface reste lisible à toutes les tailles de fenêtre.
- [ ] Uniformiser les textes, les espacements et les arrondis sur toute l'application.

## MVP - Robustesse

- [x] Gérer les erreurs réseau sans faire planter l'application.
- [ ] Gérer les réponses API incomplètes ou inattendues.
- [ ] Prévoir un mode hors ligne avec le dernier état connu, si possible.
- [ ] Journaliser les erreurs de façon simple et exploitable.
- [ ] Conserver un état cohérent si l'API devient indisponible pendant le suivi.

## MVP - Cache local

- [ ] Sauvegarder les dernières données utiles localement.
- [ ] Recharger le dernier état connu au démarrage si nécessaire.
- [ ] Éviter les appels API inutiles sur les données stables.
- [ ] Préparer un nettoyage simple des données de cache.

## MVP - Build et livraison

- [x] Garder `build.ps1` comme build complet de référence.
- [x] Produire systématiquement les builds complets dans `dist/RA.Compagnon`.
- [x] Vérifier le contenu du livrable complet après chaque build important.
- [x] Vérifier la présence et l'affichage de l'icône dans le livrable final.

## Après MVP

- [ ] Ajouter des vues complémentaires si le suivi principal est stable.
- [ ] Prévoir plusieurs écrans ou onglets si cela améliore vraiment l'usage.
- [ ] Ajouter des statistiques ou des historiques plus avancés.
- [ ] Étudier une surveillance plus riche de plusieurs états RetroAchievements.
