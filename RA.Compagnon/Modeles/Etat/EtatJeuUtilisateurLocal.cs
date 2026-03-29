namespace RA.Compagnon.Modeles.Etat;

public sealed class EtatJeuUtilisateurLocal
{
    public int GameId { get; set; }

    public DateTimeOffset DerniereObservationUtc { get; set; }

    public int NbSuccesDebloques { get; set; }

    public int NbSuccesDebloquesHardcore { get; set; }

    public double ProgressionPourcentage { get; set; }

    public int DernierSuccesDetecteId { get; set; }

    public string DernierSuccesDetecteUtc { get; set; } = string.Empty;

    public List<EtatSuccesUtilisateurLocal> Succes { get; set; } = [];
}
