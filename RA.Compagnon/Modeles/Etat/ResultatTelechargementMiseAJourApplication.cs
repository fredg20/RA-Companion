/*
 * Représente le résultat d'un téléchargement de package de mise à jour.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Transporte le succès de l'opération, l'emplacement du fichier et le
 * message utilisateur associé.
 */
public sealed record ResultatTelechargementMiseAJourApplication(
    bool Reussi,
    bool DejaPresent,
    string? CheminFichier,
    string Message
);
