/*
 * Représente le résultat de lancement de l'installation d'une mise à jour.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Transporte l'état de réussite et le message associé au lancement de
 * l'installateur de mise à jour.
 */
public sealed record ResultatLancementInstallationMiseAJourApplication(bool Reussi, string Message);