namespace RA.Compagnon.Modeles.Etat;

public sealed record ResultatTelechargementMiseAJourApplication(
    bool Reussi,
    bool DejaPresent,
    string? CheminFichier,
    string Message
);
