namespace RA.Compagnon.Modeles.Etat;

public sealed class EtatUtilisateurJeuxLocal
{
    public DateTimeOffset DateMajUtc { get; set; }

    public List<EtatJeuUtilisateurLocal> Jeux { get; set; } = [];
}
