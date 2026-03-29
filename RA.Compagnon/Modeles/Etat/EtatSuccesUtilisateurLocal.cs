namespace RA.Compagnon.Modeles.Etat;

public sealed class EtatSuccesUtilisateurLocal
{
    public int AchievementId { get; set; }

    public bool EstDebloque { get; set; }

    public bool EstHardcore { get; set; }

    public string DateDeblocageUtc { get; set; } = string.Empty;

    public string DateDeblocageHardcoreUtc { get; set; } = string.Empty;
}
