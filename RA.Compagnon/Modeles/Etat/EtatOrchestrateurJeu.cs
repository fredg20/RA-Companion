namespace RA.Compagnon.Modeles.Etat;

public sealed record EtatOrchestrateurJeu(
    PhaseOrchestrateurJeu Phase,
    int IdentifiantJeu,
    string TitreJeu,
    string Source,
    DateTimeOffset HorodatageUtc
)
{
    public static EtatOrchestrateurJeu Initial =>
        new(PhaseOrchestrateurJeu.AucunJeu, 0, string.Empty, string.Empty, DateTimeOffset.MinValue);

    public bool ConcerneJeu(int identifiantJeu)
    {
        return identifiantJeu > 0 && IdentifiantJeu == identifiantJeu;
    }
}
