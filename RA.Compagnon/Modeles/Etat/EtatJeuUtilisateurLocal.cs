/*
 * Représente l'état local observé d'un jeu utilisateur pour le cache de
 * progression et de détection de succès.
 */
namespace RA.Compagnon.Modeles.Etat;

/*
 * Stocke les métriques locales de progression d'un jeu ainsi que l'état
 * détaillé de ses succès observés.
 */
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