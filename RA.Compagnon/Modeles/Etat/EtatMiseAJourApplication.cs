namespace RA.Compagnon.Modeles.Etat;

public enum StatutMiseAJourApplication
{
    EnAttente,
    AJour,
    MiseAJourDisponible,
    VerificationImpossible,
    NonConfiguree,
}

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
    public bool MiseAJourDisponible => Statut == StatutMiseAJourApplication.MiseAJourDisponible;

    public bool PeutTelecharger =>
        MiseAJourDisponible && !string.IsNullOrWhiteSpace(UrlTelechargement);

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
