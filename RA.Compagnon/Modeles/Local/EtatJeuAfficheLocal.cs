/*
 * Représente l'état persistant d'un jeu déjà affiché afin de pouvoir
 * restaurer rapidement l'interface au démarrage.
 */
namespace RA.Compagnon.Modeles.Local;

/*
 * Stocke la version locale sérialisable du jeu affiché avec ses métadonnées
 * utiles à la restauration et au rejeu.
 */
public sealed class EtatJeuAfficheLocal
{
    public string SignatureLocale { get; set; } = string.Empty;

    public int IdentifiantJeu { get; set; }

    public bool EstJeuEnCours { get; set; }

    public string Titre { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string ResumeProgression { get; set; } = "-- / -- succès";

    public string PourcentageProgression { get; set; } = string.Empty;

    public double ValeurProgression { get; set; }

    public string TempsJeuSousImage { get; set; } = string.Empty;

    public string EtatJeu { get; set; } = string.Empty;

    public string CheminImageBoite { get; set; } = string.Empty;

    public int IdentifiantConsole { get; set; }

    public string DateSortie { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Developpeur { get; set; } = string.Empty;

    public string NomEmulateurRelance { get; set; } = string.Empty;

    public string CheminExecutableEmulateur { get; set; } = string.Empty;

    public string CheminJeuLocal { get; set; } = string.Empty;

    public int Id
    {
        get => IdentifiantJeu;
        set => IdentifiantJeu = value;
    }

    public string Title
    {
        get => Titre;
        set => Titre = value;
    }

    public string ImageBoxArt
    {
        get => CheminImageBoite;
        set => CheminImageBoite = value;
    }

    public int ConsoleId
    {
        get => IdentifiantConsole;
        set => IdentifiantConsole = value;
    }

    public string Released
    {
        get => DateSortie;
        set => DateSortie = value;
    }

    public string Developer
    {
        get => Developpeur;
        set => Developpeur = value;
    }
}
