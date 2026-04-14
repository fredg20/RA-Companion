/*
 * Déclare les états et données utilisés pour représenter la situation
 * de mise à jour de l'application.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Représente les différents statuts possibles du cycle de mise à jour.
 */
public enum StatutMiseAJourApplication
{
    EnAttente,
    AJour,
    MiseAJourDisponible,
    VerificationImpossible,
    NonConfiguree,
}

/*
 * Transporte l'état complet d'une vérification de mise à jour et les
 * informations nécessaires à l'interface.
 */
public sealed record EtatMiseAJourApplication(
    string VersionLocale,
    string? VersionDistante,
    string? UrlTelechargement,
    string? Notes,
    string? DatePublication,
    StatutMiseAJourApplication Statut,
    string Message
)
{
    /*
     * Indique si une nouvelle version est actuellement disponible.
     */
    public bool MiseAJourDisponible => Statut == StatutMiseAJourApplication.MiseAJourDisponible;

    /*
     * Indique si l'application peut proposer un téléchargement immédiat.
     */
    public bool PeutTelecharger =>
        MiseAJourDisponible && !string.IsNullOrWhiteSpace(UrlTelechargement);

    /*
     * Construit l'état initial affiché avant toute vérification distante.
     */
    public static EtatMiseAJourApplication CreerEtatInitial(string versionLocale)
    {
        return new EtatMiseAJourApplication(
            versionLocale,
            null,
            null,
            null,
            null,
            StatutMiseAJourApplication.EnAttente,
            "Vérification de la mise à jour en attente."
        );
    }
}
