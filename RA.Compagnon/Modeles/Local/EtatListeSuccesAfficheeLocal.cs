namespace RA.Compagnon.Modeles.Local;

public sealed class EtatListeSuccesAfficheeLocal
{
    public int IdentifiantJeu { get; set; }

    public List<ElementListeSuccesAfficheLocal> Succes { get; set; } = [];

    public List<int> SuccesPasses { get; set; } = [];

    public int Id
    {
        get => IdentifiantJeu;
        set => IdentifiantJeu = value;
    }

    public List<ElementListeSuccesAfficheLocal> Achievements
    {
        get => Succes;
        set => Succes = value;
    }
}
